namespace be_digitalin.Config;

public class RecaptchaSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string VerifyUrl { get; set; } = string.Empty;
    public double LoginMinimumScore { get; set; } = 0.5;
    public double RegisterMinimumScore { get; set; } = 0.6;
    public double AdminLoginMinimumScore { get; set; } = 0.7;
}