using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers;

public class MidtransProvider
{
    private readonly HttpClient _httpClient;
    private readonly MidtransSettings _settings;
    private readonly DigitalinDbContext _db;

    public MidtransProvider(HttpClient httpClient, IOptions<MidtransSettings> settings, DigitalinDbContext db)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _db = db;
    }

    // Fungsi nembak API untuk dapat Token Pop-Up UI
    public async Task<(string? Token, string? RedirectUrl)> CreateSnapTransactionAsync(
        string orderNo, decimal grossAmount, string productId, string productName, string customerName)
    {
        var expirySetting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == AppConstants.SystemSettings.MidtransExpiry);
        int expiryDuration = expirySetting != null ? int.Parse(expirySetting.Value) : 30;
        
        var payload = new
        {
            transaction_details = new { order_id = orderNo, gross_amount = (int)grossAmount },
            item_details = new[] { new { id = productId, price = (int)grossAmount, quantity = 1, name = productName } },
            customer_details = new { first_name = customerName },
            expiry = new { duration = expiryDuration, unit = AppConstants.TimeUnits.Minute }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var serverKeyBytes = Encoding.UTF8.GetBytes($"{_settings.ServerKey}:");
        var base64ServerKey = Convert.ToBase64String(serverKeyBytes);

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AppConstants.HttpAuthSchemes.Basic, base64ServerKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.PostAsync(_settings.BaseUrl, content);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Gagal terhubung ke Midtrans. Error: {responseString}");

        using var jsonDocument = JsonDocument.Parse(responseString);
        var root = jsonDocument.RootElement;
        
        return (
            root.GetProperty(AppConstants.MidtransResponseKeys.Token).GetString(), 
            root.GetProperty(AppConstants.MidtransResponseKeys.RedirectUrl).GetString()
        );
    }

    // ── [C5] Timing-safe signature validation ─────────────────────────────
    // Uses CryptographicOperations.FixedTimeEquals to prevent timing side-channel
    // attacks where an attacker can infer how many bytes matched by measuring
    // the response time of a normal string comparison.
    public bool ValidateSignature(string orderId, string statusCode, string grossAmount, string signatureKey)
    {
        // Reject immediately if any input is null/empty
        if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(statusCode) ||
            string.IsNullOrEmpty(grossAmount) || string.IsNullOrEmpty(signatureKey))
        {
            return false;
        }

        string rawSignature = orderId + statusCode + grossAmount + _settings.ServerKey;
        using var sha512 = SHA512.Create();
        byte[] hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(rawSignature));
        string calculatedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        // Timing-safe comparison: always compares the full byte arrays regardless
        // of where the first mismatch occurs.
        byte[] expectedBytes = Encoding.UTF8.GetBytes(calculatedSignature);
        byte[] actualBytes   = Encoding.UTF8.GetBytes(signatureKey);

        if (expectedBytes.Length != actualBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}