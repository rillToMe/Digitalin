namespace be_digitalin.Services;

public interface IAdminAuthService
{
    Task<string?> LoginAsync(
        string username,
        string password,
        string captchaToken,
        string? remoteIp = null,
        CancellationToken cancellationToken = default);
}