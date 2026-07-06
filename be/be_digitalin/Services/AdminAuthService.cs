using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace be_digitalin.Services;

public class AdminAuthService : IAdminAuthService
{
    private readonly DigitalinDbContext _db;
    private readonly IRecaptchaService _recaptchaService;
    private readonly RecaptchaSettings _recaptchaSettings;
    private readonly JwtSettings _jwtSettings;

    public AdminAuthService(
        DigitalinDbContext db,
        IRecaptchaService recaptchaService,
        IOptions<RecaptchaSettings> recaptchaSettings,
        IOptions<JwtSettings> jwtSettings)
    {
        _db = db;
        _recaptchaService = recaptchaService;
        _recaptchaSettings = recaptchaSettings.Value;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<string?> LoginAsync(
        string username,
        string password,
        string captchaToken,
        string? remoteIp = null,
        CancellationToken cancellationToken = default)
    {
        var captchaValid = await _recaptchaService.VerifyAsync(
            captchaToken,
            AppConstants.RecaptchaActions.AdminLogin,
            _recaptchaSettings.AdminLoginMinimumScore,
            remoteIp,
            cancellationToken);

        if (!captchaValid)
        {
            return null;
        }

        var admin = await _db.Admins.FirstOrDefaultAsync(
            a => a.Username == username,
            cancellationToken);

        if (admin == null ||
            !BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            return null;
        }

        admin.LastLoginAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return CreateAdminToken(
            admin.Id,
            admin.Username,
            admin.Role);
    }

    private string CreateAdminToken(
        int adminId,
        string username,
        string role)
    {
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Key));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                adminId.ToString()),

            new Claim(
                ClaimTypes.Name,
                username),

            new Claim(
                ClaimTypes.Role,
                role)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}