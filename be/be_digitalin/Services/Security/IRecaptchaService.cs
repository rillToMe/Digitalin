namespace be_digitalin.Services.Security;

public interface IRecaptchaService
{
    Task<bool> VerifyAsync(
        string token,
        string expectedAction,
        double minimumScore,
        string? remoteIp = null,
        CancellationToken cancellationToken = default);
}