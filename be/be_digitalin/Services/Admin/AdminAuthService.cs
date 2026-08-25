using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using be_digitalin.Services.Auth;
using be_digitalin.Services.Security;

namespace be_digitalin.Services.Admin;

public class AdminAuthService : IAdminAuthService
{
    private readonly DigitalinDbContext _db;
    private readonly IRecaptchaService _recaptchaService;
    private readonly RecaptchaSettings _recaptchaSettings;
    private readonly ITokenService _tokenService;

    public AdminAuthService(
        DigitalinDbContext db,
        IRecaptchaService recaptchaService,
        IOptions<RecaptchaSettings> recaptchaSettings,
        ITokenService tokenService)
    {
        _db = db;
        _recaptchaService = recaptchaService;
        _recaptchaSettings = recaptchaSettings.Value;
        _tokenService = tokenService;
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

        return _tokenService.CreateAdminToken(
            admin.Id,
            admin.Username,
            admin.Role);
    }
}