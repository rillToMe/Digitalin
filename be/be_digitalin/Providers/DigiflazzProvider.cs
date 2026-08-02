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
    
    private const string EndpointTransaction = "/v1/transaction";
    private const string EndpointPriceList = "/v1/price-list";
    private const string EndpointCheckBalance = "/v1/cek-saldo";

    public DigiflazzProvider(HttpClient httpClient, IOptions<DigiflazzSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        
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
                    
                    // Paksa sistem melempar error agar masuk ke blok 'catch'
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
        
        var response = await _httpClient.PostAsync(EndpointPriceList, content);
        var responseString = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseString);
        
        var dataElement = jsonDoc.RootElement.GetProperty(AppConstants.DigiflazzResponseKeys.Data);

        if (dataElement.ValueKind != JsonValueKind.Array)
        {
            throw new Exception($"[Digiflazz Error] API menolak request. Balasan: {responseString}");
        }
        
        var sampleGame = dataElement
            .EnumerateArray()
            .FirstOrDefault(item =>
                item.TryGetProperty(
                    AppConstants.DigiflazzResponseKeys.Category,
                    out var category
                ) &&
                category.GetString() == AppConstants.Categories.Games
            );

        if (sampleGame.ValueKind != JsonValueKind.Undefined)
        {
            Console.WriteLine("\n========== DIGIFLAZZ SAMPLE GAME ==========");
            Console.WriteLine(
                JsonSerializer.Serialize(
                    sampleGame,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                )
            );
            Console.WriteLine("============================================\n");
        }

        return dataElement.EnumerateArray();
    }

    /// <summary>
    /// Saldo deposit yang tersisa di akun Digiflazz. Dipakai dashboard admin
    /// supaya saldo tidak perlu dicek manual ke web supplier.
    /// </summary>
    public async Task<decimal> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            cmd = AppConstants.DigiflazzCommands.Deposit,
            username = _settings.Username,
            sign = GenerateSignature(AppConstants.DigiflazzCommands.DepositSign)
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(EndpointCheckBalance, content, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        using var jsonDoc = JsonDocument.Parse(responseString);

        if (!jsonDoc.RootElement.TryGetProperty(AppConstants.DigiflazzResponseKeys.Data, out var data))
        {
            throw new Exception($"[Digiflazz Error] Balasan cek saldo tidak dikenali: {responseString}");
        }

        // Sukses dan gagal memakai bentuk yang sama; kalau kredensial salah atau
        // IP belum di-whitelist, "deposit" tidak ada dan hanya "message" yang datang.
        if (!data.TryGetProperty(AppConstants.DigiflazzResponseKeys.Deposit, out var deposit))
        {
            var message = data.TryGetProperty(AppConstants.DigiflazzResponseKeys.Message, out var msgElement)
                ? msgElement.GetString()
                : null;

            throw new Exception(message ?? $"[Digiflazz Error] Cek saldo gagal: {responseString}");
        }

        return deposit.GetDecimal();
    }

    // webhook validasi signature
    public bool ValidateWebhookSignature(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(signatureHeader))
            return false;
        
        if (!signatureHeader.StartsWith(AppConstants.SecurityPrefixes.Sha1, StringComparison.Ordinal))
            return false;

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_settings.WebhookSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expectedSignature = AppConstants.SecurityPrefixes.Sha1 + Convert.ToHexString(hashBytes).ToLower();

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        byte[] actualBytes   = Encoding.UTF8.GetBytes(signatureHeader);

        if (expectedBytes.Length != actualBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
    
    //  Bot Patroli membaca status real-time
    public async Task<(string Status, string Sn, string Message)> CheckStatusAsync(string sku, string customerNo, string refId)
    {
        var payload = new
        {
            username = _settings.Username,
            buyer_sku_code = sku,
            customer_no = customerNo,
            ref_id = refId,
            sign = GenerateSignature(refId)
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