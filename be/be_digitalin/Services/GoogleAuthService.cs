using System.Security.Claims;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Models;
using be_digitalin.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace be_digitalin.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleProvider _googleProvider;
    private readonly DigitalinDbContext _db;
    private readonly JwtSettings _jwtSettings;

    public GoogleAuthService(
        GoogleProvider googleProvider,
        DigitalinDbContext db,
        IOptions<JwtSettings> jwtSettings)
    {
        _googleProvider = googleProvider;
        _db = db;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponse?> AuthenticateWithGoogleAsync(string idToken)
    {
        // 1. Verify Google ID Token using the official Google SDK
        var payload = await _googleProvider.VerifyIdTokenAsync(idToken);

        var email = payload.Email;
        var googleId = payload.Subject; // Google's unique user ID (sub claim)
        var displayName = payload.Name;

        // 2. Find existing Customer by email
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);

        if (customer == null)
        {
            // 3a. Customer doesn't exist — auto-register with Google info
            customer = new Customer
            {
                Email = email,
                FullName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                GoogleId = googleId,
                AuthProvider = "GOOGLE",
                // PasswordHash stays empty — Google accounts don't use local passwords
                PasswordHash = string.Empty
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }
        else if (string.IsNullOrEmpty(customer.GoogleId))
        {
            // 3b. Existing local account with same email — link Google to it
            //     (no duplicate account created)
            customer.GoogleId = googleId;

            // If AuthProvider was not set (legacy data), mark it now
            if (string.IsNullOrEmpty(customer.AuthProvider))
                customer.AuthProvider = "LOCAL";

            await _db.SaveChangesAsync();
        }
        // 3c. Customer already has GoogleId — just proceed to login (no DB write needed)

        // 4. Generate JWT using the same mechanism as local login
        var token = GenerateJwt(customer);
        return new AuthResponse(token);
    }

    /// <summary>
    /// Generates a JWT identical in structure to the one produced by
    /// CustomerService.LoginAsync — same claims, same expiry, same signing key.
    /// </summary>
    private string GenerateJwt(Customer customer)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(ClaimTypes.Role, AppConstants.Roles.Customer)
            }),
            Expires = DateTime.UtcNow.AddHours(24),
            SigningCredentials = credentials
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }
}
