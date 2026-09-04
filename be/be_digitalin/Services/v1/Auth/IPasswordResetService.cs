namespace be_digitalin.Services.Auth;

public interface IPasswordResetService
{
    /// <summary>
    /// Finds the customer by email, generates a Forgot Password OTP,
    /// and sends it via email.
    /// Returns false if the email is not registered.
    /// </summary>
    Task<bool> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the OTP for the given email, and if valid,
    /// updates the customer's password with the hashed newPassword.
    /// Returns false if email not found, OTP invalid/expired, or password invalid.
    /// </summary>
    Task<PasswordResetResult> ResetPasswordAsync(string email, string otpCode, string newPassword, CancellationToken cancellationToken = default);
}

public enum PasswordResetResult
{
    Success,
    EmailNotFound,
    InvalidOtp
}
