namespace be_digitalin.Services;

/// <summary>
/// Generic OTP infrastructure service for all OTP-based flows.
/// Use OtpPurpose constants for the purpose parameter.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a 6-digit OTP for the given customer and purpose,
    /// persists it to the database (overwriting any existing OTP for the same purpose),
    /// and returns the plain-text OTP code.
    /// </summary>
    Task<string> GenerateAndSaveOtpAsync(int customerId, string purpose, TimeSpan? ttl = null);

    /// <summary>
    /// Validates the OTP code for the given customer and purpose.
    /// If valid, the OTP is consumed (deleted) so it cannot be reused.
    /// Returns true if valid; false if not found, mismatched, or expired.
    /// </summary>
    Task<bool> ValidateAndConsumeOtpAsync(int customerId, string purpose, string otpCode);
}
