using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using be_digitalin.Config;
using Microsoft.Extensions.Options;

namespace be_digitalin.Sandbox.Digiflazz;

public class DigiflazzSandboxClient
{
    private readonly HttpClient _httpClient;
    private readonly DigiflazzSettings _settings;
    private const string EndpointTransaction = "/v1/transaction";
    private const string EndpointPriceList = "/v1/price-list";

    public DigiflazzSandboxClient(
        HttpClient httpClient,
        IOptions<DigiflazzSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;

        _httpClient.BaseAddress ??= new Uri(_settings.BaseUrl);
    }

    /// <summary>
    /// Mengambil response mentah Price List dari Digiflazz.
    /// </summary>
    public Task<JsonDocument> GetPriceListAsync(
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            cmd = "prepaid",
            username = _settings.Username,
            sign = GenerateSignature("pricelist")
        };

        return PostAsync(
            EndpointPriceList,
            payload,
            cancellationToken);
    }

    /// <summary>
    /// Mengambil response mentah Inquiry dari Digiflazz.
    /// </summary>
    public Task<JsonDocument> InquiryAsync(
        object payload,
        CancellationToken cancellationToken = default)
    {
        return PostAsync(
            EndpointTransaction,
            payload,
            cancellationToken);
    }

    /// <summary>
    /// Mengambil response mentah Topup dari Digiflazz.
    /// </summary>
    public Task<JsonDocument> TransactionAsync(
        object payload,
        CancellationToken cancellationToken = default)
    {
        return PostAsync(
            EndpointTransaction,
            payload,
            cancellationToken);
    }

    // ======================================================
    // PRIVATE
    // ======================================================

    private async Task<JsonDocument> PostAsync(
        string endpoint,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync(
            endpoint,
            content,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"HTTP {(int)response.StatusCode}\n\n{responseBody}");
        }

        return JsonDocument.Parse(responseBody);
    }

    private string GenerateSignature(string command)
    {
        var raw = $"{_settings.Username}{_settings.ApiKey}{command}";

        var hash = MD5.HashData(
            Encoding.UTF8.GetBytes(raw));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}