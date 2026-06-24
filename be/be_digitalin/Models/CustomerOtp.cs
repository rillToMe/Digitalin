namespace be_digitalin.Models;

/// <summary>
/// Generic OTP record for all OTP-based flows:
/// FORGOT_PASSWORD, EMAIL_VERIFY, CHANGE_EMAIL, SECURITY_VERIFY, etc.
///
/// Only ONE active OTP per (CustomerId + Purpose) is allowed at a time.
/// A new request overwrites the previous one.
/// </summary>
public class CustomerOtp
{
    public int Id { get; set; }

    /// <summary>Foreign key to Customer.</summary>
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// OTP purpose identifier.
    /// Use OtpPurpose constants to avoid magic strings.
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>6-digit numeric OTP code.</summary>
    public string OtpCode { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this OTP expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC timestamp when this OTP was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
