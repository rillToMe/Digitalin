namespace be_digitalin.Application.Ports;

/// <summary>
/// Port email (OTP & password reset). Implemented by
/// <c>be_digitalin.Providers.EmailProvider</c>.
/// </summary>
public interface IEmailProvider
{
    Task SendOtpEmailAsync(
        string targetEmail,
        string otpCode,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetOtpEmailAsync(
        string targetEmail,
        string otpCode,
        CancellationToken cancellationToken = default);
}