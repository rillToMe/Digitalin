using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace be_digitalin.Tests.Services;

public class TokenServiceTests
{
    private const string Issuer = "digitalin-issuer";
    private const string Audience = "digitalin-audience";
    // Kunci HMAC-SHA256 minimal 256-bit (>= 32 byte).
    private const string Key = "super-secret-signing-key-for-unit-tests-0123456789";

    private static TokenService CreateService()
    {
        var settings = new JwtSettings
        {
            Issuer = Issuer,
            Audience = Audience,
            Key = Key
        };
        return new TokenService(Options.Create(settings));
    }

    // Validasi & baca token yang dihasilkan menggunakan handler yang sama
    // dengan konfigurasi produksi (lihat Program.cs).
    private static ClaimsPrincipal ReadAndValidate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            ClockSkew = TimeSpan.Zero
        };

        return handler.ValidateToken(token, parameters, out _);
    }

    [Fact]
    public void CreateAdminToken_ContainsExpectedClaims()
    {
        var service = CreateService();

        var token = service.CreateAdminToken(42, "adminuser", AppConstants.Roles.SuperAdmin);

        var principal = ReadAndValidate(token);

        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("42");
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("adminuser");
        principal.FindFirst(ClaimTypes.Role)!.Value.Should().Be(AppConstants.Roles.SuperAdmin);
    }

    [Fact]
    public void CreateCustomerToken_ContainsExpectedClaims()
    {
        var service = CreateService();

        var token = service.CreateCustomerToken(7);

        var principal = ReadAndValidate(token);

        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("7");
        principal.FindFirst(ClaimTypes.Role)!.Value.Should().Be(AppConstants.Roles.Customer);
        // Customer token tidak menyertakan claim Name.
        principal.FindFirst(ClaimTypes.Name).Should().BeNull();
    }

    [Fact]
    public void CreateAdminToken_UsesConfiguredIssuerAndAudience()
    {
        var service = CreateService();

        var token = service.CreateAdminToken(1, "x", AppConstants.Roles.Admin);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Contain(Audience);
    }

    [Fact]
    public void CreateAdminToken_HasTwelveHourLifetime()
    {
        var service = CreateService();

        var token = service.CreateAdminToken(1, "x", AppConstants.Roles.Admin);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        var lifetime = jwt.ValidTo - jwt.ValidFrom;
        lifetime.Should().BeCloseTo(TimeSpan.FromHours(12), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void CreateCustomerToken_HasTwentyFourHourLifetime()
    {
        var service = CreateService();

        var token = service.CreateCustomerToken(1);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        var lifetime = jwt.ValidTo - jwt.ValidFrom;
        lifetime.Should().BeCloseTo(TimeSpan.FromHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Token_SignedWithWrongKey_FailsValidation()
    {
        var service = CreateService();
        var token = service.CreateCustomerToken(1);

        var handler = new JwtSecurityTokenHandler();
        var wrongKeyParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("a-totally-different-wrong-key-0123456789-xx")),
        };

        var act = () => handler.ValidateToken(token, wrongKeyParams, out _);
        act.Should().Throw<SecurityTokenException>();
    }
}
