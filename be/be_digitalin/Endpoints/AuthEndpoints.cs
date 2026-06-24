using System.IdentityModel.Tokens.Jwt;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Services;
using be_digitalin.Providers; // <-- Jangan lupa Provider Email
using be_digitalin.DTOs.Requests;
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
        // AREA PUBLIK
        var customerGroup = app.MapGroup("/auth").WithTags("Otentikasi (Customer)");

        var localAuthGroup = customerGroup.MapGroup("/local");

        // ENDPOINT MINTA OTP
        localAuthGroup.MapPost("/request-otp", async (
            [FromBody] RequestOtpRequest req, 
            [FromServices] EmailProvider emailProvider, 
            [FromServices] ICustomerService service,
            [FromServices] IMemoryCache cache) =>
        {
            var emailSudahTerpakai = await service.CheckEmailExistsAsync(req.Email);
            if (emailSudahTerpakai)
            {
                return Results.BadRequest(new { message = "Email sudah terdaftar. Silakan login." });
            }

            int otpNumber = RandomNumberGenerator.GetInt32(100_000, 999_999);
            string otpCode = otpNumber.ToString();

            cache.Set($"OTP_{req.Email}", otpCode, TimeSpan.FromMinutes(5));
            await emailProvider.SendOtpEmailAsync(req.Email, otpCode);
            return Results.Ok(new { message = "Kode OTP berhasil dikirim ke email Anda. Berlaku 5 menit." });
        }).RequireRateLimiting("OtpPolicy");

        // ENDPOINT REGISTER
        localAuthGroup.MapPost("/register", async (
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

        // ENDPOINT LOGIN CUSTOMER
        localAuthGroup.MapPost("/login", async ([FromBody] CustomerLoginRequest req, [FromServices] ICustomerService service) =>
        {
            var token = await service.LoginAsync(req.Email, req.Password);
            return token != null ? Results.Ok(new { token }) : Results.Unauthorized();
        });

        // ENDPOINT REQUEST RESET PASSWORD
        localAuthGroup.MapPost("/request-password-reset", async (
            [FromBody] RequestPasswordResetRequest req,
            [FromServices] IPasswordResetService passwordResetService) =>
        {
            // Manual validation (Minimal API doesn't auto-validate DataAnnotations)
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(req, new ValidationContext(req), validationResults, validateAllProperties: true))
            {
                return Results.BadRequest(new { message = validationResults[0].ErrorMessage });
            }

            var success = await passwordResetService.RequestPasswordResetAsync(req.Email);

            if (!success)
            {
                return Results.BadRequest(new { message = "Email tidak terdaftar." });
            }

            return Results.Ok(new { message = "Kode OTP reset password berhasil dikirim." });
        }).RequireRateLimiting("OtpPolicy");

        // ENDPOINT RESET PASSWORD
        localAuthGroup.MapPost("/reset-password", async (
            [FromBody] ResetPasswordRequest req,
            [FromServices] IPasswordResetService passwordResetService) =>
        {
            // Manual validation
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(req, new ValidationContext(req), validationResults, validateAllProperties: true))
            {
                return Results.BadRequest(new { message = validationResults[0].ErrorMessage });
            }

            var result = await passwordResetService.ResetPasswordAsync(req.Email, req.OtpCode, req.NewPassword);

            return result switch
            {
                PasswordResetResult.Success      => Results.Ok(new { message = "Password berhasil diubah." }),
                PasswordResetResult.EmailNotFound => Results.BadRequest(new { message = "Email tidak terdaftar." }),
                PasswordResetResult.InvalidOtp    => Results.BadRequest(new { message = "Kode OTP salah atau sudah kedaluwarsa." }),
                _                                => Results.Problem("Terjadi kesalahan yang tidak diketahui.")
            };
        });

        // GOOGLE LOGIN ENDPOINT
        // Verifies a Google ID Token, auto-registers if needed, returns JWT.
        customerGroup.MapPost("/google", async (
            [FromBody] GoogleLoginRequest req,
            [FromServices] IGoogleAuthService googleAuthService) =>
        {
            if (string.IsNullOrWhiteSpace(req.IdToken))
            {
                return Results.BadRequest(new { message = "ID token tidak boleh kosong." });
            }

            try
            {
                var result = await googleAuthService.AuthenticateWithGoogleAsync(req.IdToken);
                if (result == null)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new { token = result.Token });
            }
            catch (GoogleAuthenticationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });


        // GET PROFILE
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

        // PUT PROFILE
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
        
        // ENDPOINT CHECK EMAIL
        customerGroup.MapGet("/check-email", async (
            [FromQuery] string email, 
            [FromServices] ICustomerService service) =>
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Results.BadRequest(new { message = "Email tidak boleh kosong." });
            }
            
            var emailSudahTerpakai = await service.CheckEmailExistsAsync(email);
            
            if (emailSudahTerpakai)
            {
                return Results.BadRequest(new { message = "Email sudah terdaftar. Silakan login." });
            }
            return Results.Ok(new { message = "Email tersedia." });
        });
        
        // AREA ADMIN 
        var group = app.MapGroup("/admin/auth")
            .WithTags("Otentikasi (Admin)")
            .RequireAuthorization("AdminOnly");

        // Admin login
        group.MapPost("/login", (
            [FromBody] LoginRequest request, 
            [FromServices] IOptions<JwtSettings> jwtOptions) =>
        {
            var settings = jwtOptions.Value;

            if (request.Username != settings.AdminUser ||
                !BCrypt.Net.BCrypt.Verify(request.Password, settings.AdminPass))
            {
                return Results.Unauthorized();
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            
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
        }).AllowAnonymous(); 
    }
}

// DTO
public record LoginRequest(string Username, string Password);
public record CustomerLoginRequest(string Email, string Password);
public record RequestOtpRequest(string Email);
public record RegisterRequest(string Email, string Password, string FullName, string OtpCode);
public record UpdateProfileRequest(string FullName, string? NewPassword);
public record GoogleLoginRequest(string IdToken);
