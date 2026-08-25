using be_digitalin.Providers;

namespace be_digitalin.Services.Security;

public class RecaptchaService : IRecaptchaService
{
    private readonly RecaptchaProvider _recaptchaProvider;
    private readonly ILogger<RecaptchaService> _logger;

    public RecaptchaService(
        RecaptchaProvider recaptchaProvider,
        ILogger<RecaptchaService> logger)
    {
        _recaptchaProvider = recaptchaProvider;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(
        string token,
        string expectedAction,
        double minimumScore,
        string? remoteIp = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var result = await _recaptchaProvider.VerifyAsync(
            token,
            remoteIp,
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "[RECAPTCHA] Verifikasi gagal. Errors: {Errors}",
                string.Join(", ", result.ErrorCodes));

            return false;
        }

        if (!string.Equals(
                result.Action,
                expectedAction,
                StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "[RECAPTCHA] Action tidak sesuai. Expected: {ExpectedAction}, Actual: {ActualAction}",
                expectedAction,
                result.Action);

            return false;
        }

        if (result.Score < minimumScore)
        {
            _logger.LogWarning(
                "[RECAPTCHA] Score terlalu rendah. Action: {Action}, Score: {Score}, Minimum: {MinimumScore}",
                result.Action,
                result.Score,
                minimumScore);

            return false;
        }

        return true;
    }
}