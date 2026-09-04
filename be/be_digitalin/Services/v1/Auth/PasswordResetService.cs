using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Providers;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Auth;

public class PasswordResetService : IPasswordResetService
{
    private readonly DigitalinDbContext _db;
    private readonly IOtpService _otpService;
    private readonly EmailProvider _emailProvider;

    public PasswordResetService(
        DigitalinDbContext db,
        IOtpService otpService,
        EmailProvider emailProvider)
    {
        _db            = db;
        _otpService    = otpService;
        _emailProvider = emailProvider;
    }

    /// <inheritdoc />
    public async Task<bool> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Email == email, cancellationToken);

        if (customer == null) return false;

        // Generate OTP and persist to DB (overwrites any previous FORGOT_PASSWORD OTP)
        var otpCode = await _otpService.GenerateAndSaveOtpAsync(
            customer.Id,
            OtpPurpose.ForgotPassword);

        // Send OTP via email using the dedicated password-reset template
        await _emailProvider.SendPasswordResetOtpEmailAsync(email, otpCode, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<PasswordResetResult> ResetPasswordAsync(
        string email, string otpCode, string newPassword, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Email == email, cancellationToken);

        if (customer == null) return PasswordResetResult.EmailNotFound;

        // Validate and consume OTP (single-use)
        bool valid = await _otpService.ValidateAndConsumeOtpAsync(
            customer.Id,
            OtpPurpose.ForgotPassword,
            otpCode);

        if (!valid) return PasswordResetResult.InvalidOtp;

        // Update password
        customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();

        return PasswordResetResult.Success;
    }
}


