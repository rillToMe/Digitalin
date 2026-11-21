using System.Security.Claims;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Constants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using be_digitalin.Services.Auth;
namespace be_digitalin.Infrastructure.Security;

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    // Token lifetimes — kept as named constants so the intent is explicit.
    private static readonly TimeSpan AdminLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan CustomerLifetime = TimeSpan.FromHours(24);

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string CreateAdminToken(int adminId, string username, string role)
    {
        var claims = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        });

        return CreateToken(claims, AdminLifetime);
    }

    public string CreateCustomerToken(int customerId)
    {
        var claims = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customerId.ToString()),
            new Claim(ClaimTypes.Role, AppConstants.Roles.Customer)
        });

        return CreateToken(claims, CustomerLifetime);
    }

    private string CreateToken(ClaimsIdentity claims, TimeSpan lifetime)
    {
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Key));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            Subject = claims,
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = credentials
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }
}
