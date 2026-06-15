using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Services;
using be_digitalin.Providers; // <-- Jangan lupa Provider Email
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc; 
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.Memory; // <-- Jangan lupa MemoryCache

namespace be_digitalin.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // AREA PUBLIK (Customer Auth)
        var customerGroup = app.MapGroup("/auth").WithTags("Otentikasi (Customer)");

        // ── 1. ENDPOINT MINTA OTP ──────────────────────────────────────────
        // [C4 fix] Cryptographically secure OTP via RandomNumberGenerator.
        // Rate-limited in Program.cs via the "OtpPolicy" policy (3 requests / 10 min per IP).
        customerGroup.MapPost("/request-otp", async (
            [FromBody] RequestOtpRequest req, 
            [FromServices] EmailProvider emailProvider, 
            [FromServices] IMemoryCache cache) =>
        {
            // ── [C4] Cryptographically secure OTP generation ─────────────
            // RandomNumberGenerator is NOT seeded by system time and produces
            // values that are computationally infeasible to predict.
            int otpNumber = RandomNumberGenerator.GetInt32(100_000, 999_999);
            string otpCode = otpNumber.ToString();

            cache.Set($"OTP_{req.Email}", otpCode, TimeSpan.FromMinutes(5));
            await emailProvider.SendOtpEmailAsync(req.Email, otpCode);
            return Results.Ok(new { message = "Kode OTP berhasil dikirim ke email Anda. Berlaku 5 menit." });
        }).RequireRateLimiting("OtpPolicy");

        // 2. ENDPOINT REGISTER (Sudah ditambahkan pengecekan OTP)
        customerGroup.MapPost("/register", async (
            [FromBody] RegisterRequest req, 
            [FromServices] ICustomerService service, 
            [FromServices] IMemoryCache cache) =>
        {
            // Cek OTP di RAM
            if (!cache.TryGetValue($"OTP_{req.Email}", out string? savedOtp) || savedOtp != req.OtpCode)
            {
                return Results.BadRequest(new { message = "Kode OTP salah atau sudah kedaluwarsa." });
            }

            var user = await service.RegisterAsync(req.Email, req.Password, req.FullName);
            if (user != null) 
            {
                cache.Remove($"OTP_{req.Email}"); // Bersihkan memori
                return Results.Ok(new { message = "Registrasi sukses!" });
            }
            
            return Results.BadRequest(new { message = "Email sudah terdaftar." });
        });

        // 3. ENDPOINT LOGIN CUSTOMER
        customerGroup.MapPost("/login", async ([FromBody] CustomerLoginRequest req, [FromServices] ICustomerService service) =>
        {
            var token = await service.LoginAsync(req.Email, req.Password);
            return token != null ? Results.Ok(new { token }) : Results.Unauthorized();
        });
        
        // 4. GET PROFILE (Melihat Data Sendiri)
        customerGroup.MapGet("/profile", async (ClaimsPrincipal user, [FromServices] ICustomerService service) =>
        {
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null) return Results.Unauthorized();

            var customer = await service.GetProfileAsync(int.Parse(idClaim.Value));
            if (customer == null) return Results.NotFound();

            return Results.Ok(new { 
                customer.Email, 
                customer.FullName, 
                customer.CreatetAt 
            });
        }).RequireAuthorization();

        // 5. PUT PROFILE (Update Nama & Password)
        customerGroup.MapPut("/profile", async (
            ClaimsPrincipal user, 
            [FromBody] UpdateProfileRequest req, 
            [FromServices] ICustomerService service) =>
        {
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null) return Results.Unauthorized();

            var success = await service.UpdateProfileAsync(int.Parse(idClaim.Value), req.FullName, req.NewPassword);
            
            return success 
                ? Results.Ok(new { message = "Profil berhasil diperbarui!" }) 
                : Results.NotFound();
        }).RequireAuthorization();
        
        // ── AREA ADMIN ─────────────────────────────────────────────────────
        // [C7 fix] .RequireAuthorization("AdminOnly") enforces that the JWT
        // must contain ClaimTypes.Role = "ADMIN". A plain customer token
        // will be rejected with 403.
        var group = app.MapGroup("/admin/auth")
            .WithTags("Otentikasi (Admin)")
            .RequireAuthorization("AdminOnly");

        // Admin login — the endpoint itself is public (AllowAnonymous overrides the group policy)
        group.MapPost("/login", (
            [FromBody] LoginRequest request, 
            [FromServices] IOptions<JwtSettings> jwtOptions) =>
        {
            var settings = jwtOptions.Value;

            // ── [C8] Admin password is verified via BCrypt, not plaintext ────
            // Program.cs auto-hashes the config value at startup if it isn't
            // already a BCrypt hash, so dev configs can stay readable while
            // production env vars carry a proper $2a$ hash.
            if (request.Username != settings.AdminUser ||
                !BCrypt.Net.BCrypt.Verify(request.Password, settings.AdminPass))
            {
                return Results.Unauthorized();
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            
            // ── [C7] Embed the ADMIN role claim in the JWT ─────────────────
            // This is what the "AdminOnly" authorization policy checks for.
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = settings.Issuer,
                Audience = settings.Audience,
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, request.Username),
                    new Claim(ClaimTypes.Role, AppConstants.Roles.Admin)
                }),
                Expires = DateTime.UtcNow.AddHours(12),
                SigningCredentials = credentials
            };

            var handler = new JsonWebTokenHandler();
            var tokenString = handler.CreateToken(tokenDescriptor);

            return Results.Ok(new { token = tokenString });
        }).AllowAnonymous(); // Admin login must be publicly accessible
    }
}

// KUMPULAN DTO
public record LoginRequest(string Username, string Password);
public record CustomerLoginRequest(string Email, string Password);
public record RequestOtpRequest(string Email); // <-- DTO baru
public record RegisterRequest(string Email, string Password, string FullName, string OtpCode); // <-- Tambahan OtpCode
public record UpdateProfileRequest(string FullName, string? NewPassword);
