using System.Text.Json.Serialization;

namespace be_digitalin.Application.Ports;

/// <summary>
/// Port verifikasi reCAPTCHA. Implemented by
/// <c>be_digitalin.Providers.RecaptchaProvider</c>.
/// </summary>
public interface IRecaptchaProvider
{
    Task<RecaptchaVerifyResponse> VerifyAsync(
        string token,
        string? remoteIp = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Respons mentah Google reCAPTCHA siteverify.
/// (Dipindah dari Providers/RecaptchaProvider.cs — kontrak Application.)
/// JsonPropertyName wajib dipertahankan: respons Google memakai casing berbeda.
/// </summary>
public class RecaptchaVerifyResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("challenge_ts")]
    public DateTime? ChallengeTimestamp { get; set; }

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = string.Empty;

    [JsonPropertyName("error-codes")]
    public string[] ErrorCodes { get; set; } = [];
}