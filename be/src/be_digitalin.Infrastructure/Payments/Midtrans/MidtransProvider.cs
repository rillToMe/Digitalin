using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Services.Settings;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers;

public class MidtransProvider : IMidtransProvider
{
    private readonly HttpClient _httpClient;
    private readonly MidtransSettings _settings;
    private readonly IAppSettingsService _appSettings;

    public MidtransProvider(HttpClient httpClient, IOptions<MidtransSettings> settings, IAppSettingsService appSettings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _appSettings = appSettings;
    }

    // ── Webhook signature ───────────────────────────────────────────────

    public bool ValidateSignature(string orderId, string statusCode, string grossAmount, string signatureKey)
    {
        // tolak kalau input kosong
        if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(statusCode) ||
            string.IsNullOrEmpty(grossAmount) || string.IsNullOrEmpty(signatureKey))
        {
            return false;
        }

        string rawSignature = orderId + statusCode + grossAmount + _settings.ServerKey;
        using var sha512 = SHA512.Create();
        byte[] hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(rawSignature));
        string calculatedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        byte[] expectedBytes = Encoding.UTF8.GetBytes(calculatedSignature);
        byte[] actualBytes   = Encoding.UTF8.GetBytes(signatureKey);

        if (expectedBytes.Length != actualBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    // ── Core API — semua panggilan menyasar {Midtrans:CoreApiBaseUrl}/v2/... ──

    private const string CoreApiChargeEndpoint = "/v2/charge";
    private const string CoreApiStatusAction = "status";
    private const string CoreApiCancelAction = "cancel";
    private const string CoreApiExpireAction = "expire";

    // Buat transaksi Core API untuk payment_type spesifik; instruksi
    // pembayaran (QR string / nomor VA / actions) ikut di respons.
    public async Task<MidtransChargeResult> CreateChargeAsync(
        MidtransChargeRequest request, CancellationToken cancellationToken = default)
    {
        // Durasi expiry dari SSOT yang sama dengan sweep (AppSettings).
        int expiryDuration = await _appSettings.GetMidtransExpiryMinutesAsync();

        // Mandiri tidak punya VA di bank_transfer — Midtrans melayaninya
        // lewat echannel (Mandiri Bill Payment: biller_code + bill_key).
        // Pilihan bank "mandiri" dipetakan ke kanal itu. JANGAN kirim
        // bank:"mandiri" ke bank_transfer: Midtrans diam-diam membuat VA
        // PERMATA (verifikasi sandbox) — UX rusak + rekonsiliasi bias.
        const string MandiriBankCode = "mandiri";
        const string EchannelPaymentType = "echannel";
        bool isMandiriEchannel =
            request.PaymentType == AppConstants.MidtransPaymentMethods.BankTransfer &&
            request.Bank == MandiriBankCode;

        // Nominal dibulatkan ke rupiah utuh — guard nominal webhook
        // membandingkan (int)TotalPaid.
        var payload = new Dictionary<string, object?>
        {
            ["payment_type"] = isMandiriEchannel ? EchannelPaymentType : request.PaymentType,
            ["transaction_details"] = new { order_id = request.OrderNo, gross_amount = Pricing.ToRupiahInt(request.GrossAmount) },
            ["item_details"] = new[]
            {
                new { id = request.ProductId, price = Pricing.ToRupiahInt(request.GrossAmount), quantity = 1, name = request.ProductName }
            },
            // Prefix non-numerik WAJIB: notifikasi pending cstore pernah
            // datang dengan full_name di-UNQUOTE oleh pipeline acquirer
            // ("full_name":087800001233 — angka berawalan nol = JSON invalid,
            // tidak bisa diparse). Nilai berawalan huruf selalu aman.
            ["customer_details"] = new { first_name = $"Digitalin {request.CustomerName}" },
            ["expiry"] = new { duration = expiryDuration, unit = AppConstants.TimeUnits.Minute }
        };

        // Detail kanal diletakkan di bawah key payment_type-nya, mis.
        // "bank_transfer": { "bank": "bca" } untuk VA.
        if (!string.IsNullOrWhiteSpace(request.Bank) && !isMandiriEchannel)
            payload[request.PaymentType] = new { bank = request.Bank };

        // Mandiri Bill: label yang tampil di ATM/m-banking pelanggan.
        // bill_info1 maks 10 karakter, bill_info2 maks 30 (dipotong Midtrans).
        if (isMandiriEchannel)
            payload[EchannelPaymentType] = new
            {
                bill_info1 = "Digitalin",
                bill_info2 = request.OrderNo
            };

        // cstore (Indomaret/Alfamart): kode bayar di kasir. Midtrans
        // MENGABAIKAN expiry custom di kanal ini (fixed 24 jam, verifikasi
        // sandbox) — jangan kirim expiry untuk cstore; OrderExpiryPolicy
        // yang menjaga sweep/invoice tetap 24 jam.
        bool isCstore = request.PaymentType == AppConstants.MidtransPaymentMethods.Cstore;
        if (isCstore)
        {
            payload.Remove("expiry");
            payload[AppConstants.MidtransPaymentMethods.Cstore] = new
            {
                store = request.Store,
                // Label di POS kasir — maks 20 karakter (dipotong Midtrans).
                message = "Topup Digitalin"
            };
        }

        // ShopeePay mewajibkan callback_url (redirect user kembali setelah
        // bayar di aplikasi Shopee) — targetkan halaman status FE yang sudah
        // publik + polling. GoPay/QRIS tidak mewajibkannya: biarkan apa adanya.
        if (request.PaymentType == AppConstants.MidtransPaymentMethods.ShopeePay)
        {
            payload[AppConstants.MidtransPaymentMethods.ShopeePay] = new
            {
                callback_url = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/order/status/{request.OrderNo}"
            };
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, _settings.CoreApiBaseUrl.TrimEnd('/') + CoreApiChargeEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        AddServerKeyAuth(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Gagal membuat transaksi Midtrans Core API. Error: {responseString}");

        using var jsonDocument = JsonDocument.Parse(responseString);
        return ParseChargeResult(jsonDocument.RootElement);
    }

    // Ambil status transaksi dari sisi Midtrans. Null = transaksi tidak
    // dikenal (404) — bukan error sistem.
    public async Task<MidtransTransactionStatus?> GetStatusAsync(
        string orderNo, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get, OrderActionUrl(orderNo, CoreApiStatusAction));
        AddServerKeyAuth(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Gagal mengecek status transaksi Midtrans. Error: {responseString}");

        using var jsonDocument = JsonDocument.Parse(responseString);
        var root = jsonDocument.RootElement;

        return new MidtransTransactionStatus(
            TransactionId: GetString(root, AppConstants.MidtransResponseKeys.TransactionId),
            OrderId: GetString(root, AppConstants.MidtransResponseKeys.OrderId),
            PaymentType: GetString(root, AppConstants.MidtransResponseKeys.PaymentType),
            TransactionStatus: GetString(root, AppConstants.MidtransResponseKeys.TransactionStatus),
            FraudStatus: GetString(root, AppConstants.MidtransResponseKeys.FraudStatus),
            StatusCode: GetString(root, AppConstants.MidtransResponseKeys.StatusCode),
            GrossAmount: GetString(root, AppConstants.MidtransResponseKeys.GrossAmount),
            SettlementTime: GetString(root, AppConstants.MidtransResponseKeys.SettlementTime));
    }

    // Batalkan transaksi sebelum settlement. False = Midtrans menolak secara
    // sadar (mis. sudah settlement / order tidak dikenal), bukan error sistem.
    public Task<bool> CancelAsync(string orderNo, CancellationToken cancellationToken = default) =>
        PostOrderActionAsync(orderNo, CoreApiCancelAction, cancellationToken);

    // Paksa transaksi PENDING hangus lebih awal. Semantik hasil sama dengan Cancel.
    public Task<bool> ExpireAsync(string orderNo, CancellationToken cancellationToken = default) =>
        PostOrderActionAsync(orderNo, CoreApiExpireAction, cancellationToken);

    private async Task<bool> PostOrderActionAsync(
        string orderNo, string action, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, OrderActionUrl(orderNo, action));
        AddServerKeyAuth(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
            return true;

        // 4xx = penolakan semantik Midtrans — hasil normal untuk caller.
        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            return false;

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new Exception($"Gagal memanggil {action} Midtrans untuk {orderNo}. Error: {responseString}");
    }

    private string OrderActionUrl(string orderNo, string action) =>
        $"{_settings.CoreApiBaseUrl.TrimEnd('/')}/v2/{Uri.EscapeDataString(orderNo)}/{action}";

    // Header per-request — bukan DefaultRequestHeaders yang terakumulasi.
    private void AddServerKeyAuth(HttpRequestMessage request)
    {
        var serverKeyBytes = Encoding.UTF8.GetBytes($"{_settings.ServerKey}:");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            AppConstants.HttpAuthSchemes.Basic, Convert.ToBase64String(serverKeyBytes));
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static MidtransChargeResult ParseChargeResult(JsonElement root)
    {
        var actions = new List<MidtransAction>();
        if (root.TryGetProperty(AppConstants.MidtransResponseKeys.Actions, out var actionsElement) &&
            actionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actionsElement.EnumerateArray())
            {
                actions.Add(new MidtransAction(
                    GetString(action, AppConstants.MidtransResponseKeys.ActionName) ?? string.Empty,
                    GetString(action, AppConstants.MidtransResponseKeys.ActionUrl) ?? string.Empty));
            }
        }

        var vaNumbers = new List<MidtransVaNumber>();
        if (root.TryGetProperty(AppConstants.MidtransResponseKeys.VaNumbers, out var vaElement) &&
            vaElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var va in vaElement.EnumerateArray())
            {
                vaNumbers.Add(new MidtransVaNumber(
                    GetString(va, AppConstants.MidtransResponseKeys.Bank) ?? string.Empty,
                    GetString(va, AppConstants.MidtransResponseKeys.VaNumber) ?? string.Empty));
            }
        }

        return new MidtransChargeResult(
            TransactionId: GetString(root, AppConstants.MidtransResponseKeys.TransactionId),
            PaymentType: GetString(root, AppConstants.MidtransResponseKeys.PaymentType),
            TransactionStatus: GetString(root, AppConstants.MidtransResponseKeys.TransactionStatus),
            FraudStatus: GetString(root, AppConstants.MidtransResponseKeys.FraudStatus),
            StatusCode: GetString(root, AppConstants.MidtransResponseKeys.StatusCode),
            StatusMessage: GetString(root, AppConstants.MidtransResponseKeys.StatusMessage),
            QrString: GetString(root, AppConstants.MidtransResponseKeys.QrString),
            Actions: actions,
            VaNumbers: vaNumbers,
            PermataVaNumber: GetString(root, AppConstants.MidtransResponseKeys.PermataVaNumber),
            BillKey: GetString(root, AppConstants.MidtransResponseKeys.BillKey),
            BillerCode: GetString(root, AppConstants.MidtransResponseKeys.BillerCode),
            PaymentCode: GetString(root, AppConstants.MidtransResponseKeys.PaymentCode),
            Store: GetString(root, AppConstants.MidtransResponseKeys.Store),
            MerchantId: GetString(root, AppConstants.MidtransResponseKeys.MerchantId));
    }
}