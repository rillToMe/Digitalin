namespace be_digitalin.Constants;

/// <summary>
/// Well-known OTP purpose identifiers used in the CustomerOtp table.
/// Extend this class as new OTP flows are added.
/// </summary>
public static class OtpPurpose
{
    public const string ForgotPassword  = "FORGOT_PASSWORD";
    public const string EmailVerify     = "EMAIL_VERIFY";
    public const string ChangeEmail     = "CHANGE_EMAIL";
    public const string SecurityVerify  = "SECURITY_VERIFY";
}
