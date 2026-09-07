using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Config;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers;

public class RecaptchaProvider : IRecaptchaProvider
{
    private readonly HttpClient _httpClient;
    private readonly RecaptchaSettings _settings;

    public RecaptchaProvider(
        HttpClient httpClient,
        IOptions<RecaptchaSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<RecaptchaVerifyResponse> VerifyAsync(
        string token,
        string? remoteIp = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new RecaptchaVerifyResponse
            {
                Success = false
            };
        }

        var formData = new Dictionary<string, string>
        {
            ["secret"] = _settings.SecretKey,
            ["response"] = token
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            formData["remoteip"] = remoteIp;
        }

        using var content = new FormUrlEncodedContent(formData);

        using var response = await _httpClient.PostAsync(
            _settings.VerifyUrl,
            content,
            cancellationToken);

        var responseString = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"[RECAPTCHA ERROR] Google API mengembalikan status " +
                $"{(int)response.StatusCode}: {responseString}");
        }

        var result = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(
            responseString);

        if (result == null)
        {
            throw new Exception(
                "[RECAPTCHA ERROR] Gagal membaca response dari Google API.");
        }

        return result;
    }
}
