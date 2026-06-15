using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using be_digitalin.Config;
using be_digitalin.Constants;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers;

public class DigiflazzProvider
{
    private readonly HttpClient _httpClient;
    private readonly DigiflazzSettings _settings;

    // 1. Definisikan endpoint secara global di sini
    private const string EndpointTransaction = "/v1/transaction";
    private const string EndpointPriceList = "/v1/price-list";

    public DigiflazzProvider(HttpClient httpClient, IOptions<DigiflazzSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;

        // 2. Setup BaseAddress sekali saja. 
        // Pastikan di appsettings.json _settings.BaseUrl isinya HANYA domain (contoh: "http://IP_VPS_LU:8080" atau "https://api.digiflazz.com")
        if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_settings.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        }
    }

    private string GenerateSignature(string suffix)
    {
        string rawSign = _settings.Username + _settings.ApiKey + suffix;
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(rawSign))).ToLower();
    }

    public async Task<string?> TopupAsync(string buyerSkuCode, string customerNo, string refId, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            username = _settings.Username,
            buyer_sku_code = buyerSkuCode,
            customer_no = customerNo,
            ref_id = refId,
            sign = GenerateSignature(refId)
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        
        // 4. Tinggal panggil konstantanya, otomatis nyambung ke BaseUrl
        var response = await _httpClient.PostAsync(EndpointTransaction, content, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        
        Console.WriteLine($"\n[DIGIFLAZZ RESPONSE - {refId}] : {responseString}");
        
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseString);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("data", out var dataElement))
            {
                if (dataElement.TryGetProperty("message", out var msgElement) && 
                    dataElement.TryGetProperty("status", out var statusElement) && 
                    statusElement.GetString() == "Gagal")
                {
                    string errorMsg = msgElement.GetString() ?? "Alasan tidak diketahui";
                    Console.WriteLine($"[DIGIFLAZZ ERROR] Transaksi Gagal! Pesan: {errorMsg}");
                    
                    // TAMBAHKAN BARIS INI: Paksa sistem melempar error agar masuk ke blok 'catch'
                    throw new Exception(errorMsg); 
                }
                if (dataElement.TryGetProperty("sn", out var snElement))
                {
                    return snElement.GetString();
                }
            }
        }
        catch (Exception ex)
        { 
            Console.WriteLine($"[DIGIFLAZZ PARSE ERROR] Gagal baca JSON: {ex.Message}"); 
            throw;
        }
        
        return null;
    }

    public async Task<JsonElement.ArrayEnumerator> GetPriceListAsync()
    {
        var payload = new { 
            cmd = AppConstants.DigiflazzCommands.Prepaid, 
            username = _settings.Username, 
            sign = GenerateSignature(AppConstants.DigiflazzCommands.Pricelist) 
        };
        
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        
        // 5. Ganti link hardcode menjadi konstanta
        var response = await _httpClient.PostAsync(EndpointPriceList, content);
        var responseString = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseString);
        
        var dataElement = jsonDoc.RootElement.GetProperty(AppConstants.DigiflazzResponseKeys.Data);

        if (dataElement.ValueKind != JsonValueKind.Array)
        {
            throw new Exception($"[Digiflazz Error] API menolak request. Balasan: {responseString}");
        }

        return dataElement.EnumerateArray();
    }

    // ── [C5] Timing-safe webhook signature validation ───────────────────────
    // • Rejects null/empty/missing headers before comparison
    // • Uses CryptographicOperations.FixedTimeEquals to prevent timing attacks
    public bool ValidateWebhookSignature(string rawBody, string signatureHeader)
    {
        // Guard: reject missing or empty header (previously an empty string
        // would silently pass through and produce a meaningless comparison).
        if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(signatureHeader))
            return false;

        // Guard: signature must start with the expected prefix
        if (!signatureHeader.StartsWith(AppConstants.SecurityPrefixes.Sha1, StringComparison.Ordinal))
            return false;

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_settings.WebhookSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expectedSignature = AppConstants.SecurityPrefixes.Sha1 + Convert.ToHexString(hashBytes).ToLower();

        // Timing-safe comparison: always compares the full byte arrays
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        byte[] actualBytes   = Encoding.UTF8.GetBytes(signatureHeader);

        if (expectedBytes.Length != actualBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
    
    // Fungsi khusus untuk Robot Tukang Sapu membaca status real-time
    public async Task<(string Status, string Sn, string Message)> CheckStatusAsync(string sku, string customerNo, string refId)
    {
        var payload = new
        {
            username = _settings.Username,
            buyer_sku_code = sku,
            customer_no = customerNo,
            ref_id = refId,
            sign = GenerateSignature(refId) // Pastikan fungsi GenerateSignature lu ada dan bisa dipakai
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(EndpointTransaction, content);
        var jsonString = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data))
        {
            string status = data.GetProperty("status").GetString() ?? "";
            string sn = data.TryGetProperty("sn", out var snProp) ? snProp.GetString() ?? "" : "";
            string message = data.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
            
            return (status, sn, message);
        }

        throw new Exception("Gagal membaca struktur balasan Digiflazz saat cek status.");
    }
}