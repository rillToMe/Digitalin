using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Config;
using be_digitalin.Constants;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers;

public class DigiflazzProvider : IDigiflazzProvider
{
    private readonly HttpClient _httpClient;
    private readonly DigiflazzSettings _settings;
    private readonly ILogger<DigiflazzProvider> _logger;
    
    private const string EndpointTransaction = "/v1/transaction";
    private const string EndpointPriceList = "/v1/price-list";
    private const string EndpointCheckBalance = "/v1/cek-saldo";

    public DigiflazzProvider(HttpClient httpClient, IOptions<DigiflazzSettings> settings, ILogger<DigiflazzProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        
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
        
        _logger.LogInformation("[DIGIFLAZZ RESPONSE - {RefId}] : {Response}", refId, responseString);
        
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
                    _logger.LogWarning("[DIGIFLAZZ ERROR] Transaksi Gagal! Pesan: {ErrorMsg}", errorMsg);
                    
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
            _logger.LogWarning(ex, "[DIGIFLAZZ PARSE ERROR] Gagal baca JSON respons topup.");
            throw;
        }
        
        return null;
    }

    public async Task<(bool IsValid, string? Username, string Message)> CheckUsernameAsync(
        string buyerSkuCode,
        string customerNo,
        string refId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            username = _settings.Username,
            buyer_sku_code = buyerSkuCode,
            customer_no = customerNo,
            ref_id = refId,
            sign = GenerateSignature(refId)
        };

        var response = await _httpClient.PostAsync(
            EndpointTransaction,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(responseString);
        if (!document.RootElement.TryGetProperty("data", out var data))
            throw new InvalidOperationException("Gagal membaca balasan Digiflazz.");

        var status = data.TryGetProperty("status", out var statusProperty)
            ? statusProperty.GetString()
            : null;
        var message = data.TryGetProperty("message", out var messageProperty)
            ? messageProperty.GetString() ?? "Nama akun tidak ditemukan."
            : "Nama akun tidak ditemukan.";
        var username = data.TryGetProperty("customer_name", out var usernameProperty)
            ? usernameProperty.GetString()
            : null;

        return (status == "Sukses" && !string.IsNullOrWhiteSpace(username), username, message);
    }

    public async Task<List<DigiflazzPriceItem>> GetPriceListAsync(CancellationToken cancellationToken = default)
    {
        var payload = new {
            cmd = AppConstants.DigiflazzCommands.Prepaid,
            username = _settings.Username,
            sign = GenerateSignature(AppConstants.DigiflazzCommands.Pricelist)
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(EndpointPriceList, content, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        // Parsing milik provider: JsonDocument di-dispose di sini dan caller
        // menerima DTO yang berdiri sendiri — tidak ada JsonElement yang
        // menempel pada dokumen yang sudah mati.
        using var jsonDoc = JsonDocument.Parse(responseString);
        var dataElement = jsonDoc.RootElement.GetProperty(AppConstants.DigiflazzResponseKeys.Data);

        if (dataElement.ValueKind != JsonValueKind.Array)
        {
            throw new Exception($"[Digiflazz Error] API menolak request. Balasan: {responseString}");
        }

        var items = new List<DigiflazzPriceItem>();

        foreach (var item in dataElement.EnumerateArray())
        {
            items.Add(new DigiflazzPriceItem(
                Category: item.TryGetProperty(AppConstants.DigiflazzResponseKeys.Category, out var category)
                    ? category.GetString()
                    : null,
                BuyerSkuCode: item.TryGetProperty(AppConstants.DigiflazzResponseKeys.BuyerSkuCode, out var sku)
                    ? sku.GetString() ?? string.Empty
                    : string.Empty,
                ProductName: item.TryGetProperty(AppConstants.DigiflazzResponseKeys.ProductName, out var name)
                    ? name.GetString() ?? string.Empty
                    : string.Empty,
                Price: item.TryGetProperty(AppConstants.DigiflazzResponseKeys.Price, out var price)
                    && price.ValueKind == JsonValueKind.Number
                    ? price.GetDecimal()
                    : 0m,
                Brand: item.TryGetProperty(AppConstants.DigiflazzResponseKeys.Brand, out var brand)
                    ? brand.GetString() ?? string.Empty
                    : string.Empty,
                BuyerProductStatus: item.TryGetProperty(AppConstants.DigiflazzResponseKeys.BuyerProductStatus, out var buyerStatus)
                    && buyerStatus.ValueKind == JsonValueKind.True,
                SellerProductStatus: item.TryGetProperty(AppConstants.DigiflazzResponseKeys.SellerProductStatus, out var sellerStatus)
                    && sellerStatus.ValueKind == JsonValueKind.True,
                Desc: item.TryGetProperty(AppConstants.DigiflazzResponseKeys.Desc, out var desc)
                    ? desc.GetString()
                    : null));
        }

        return items;
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
    //
    // Sesuai kontrak resmi Digiflazz (developer.digiflazz.com — Prabayar,
    // "Topup / Cek Status"): cek status dilakukan dengan mengirim ulang topup
    // menggunakan ref_id yang sama — provider mende-duplicate by ref_id.
    // Aturan provider: JANGAN dipanggil < 1 menit antar-pemanggilan untuk
    // transaksi yang sama, dan transaksi > 90 hari akan terbentuk BARU.
    public async Task<(string Status, string Sn, string Message)> CheckStatusAsync(string sku, string customerNo, string refId, CancellationToken cancellationToken = default)
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

        var response = await _httpClient.PostAsync(EndpointTransaction, content, cancellationToken);
        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);

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
