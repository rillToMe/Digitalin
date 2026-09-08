using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Config;
using be_digitalin.Providers;
using be_digitalin.Services.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace be_digitalin.Tests.Providers;

/// <summary>
/// Unit test MidtransProvider — method Core API (Phase 1 migrasi Snap → Core
/// API) tanpa HTTP sungguhan: fake handler memverifikasi URL, Basic auth
/// ServerKey, bentuk payload charge (payment_type, nominal bulat, expiry dari
/// AppSettings, detail kanal VA), parsing instruksi pembayaran (QRIS/VA/
/// permata/bill), status, cancel/expire, dan kontrak fail-fast konfigurasi.
/// </summary>
public class MidtransProviderCoreApiTests
{
    private const string ServerKey = "SB-Mid-test-server-key";
    private const string CoreApiBaseUrl = "https://api.sandbox.midtrans.com";
    private const string OrderNo = "INV-20260907-ABCDEF1234";

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public readonly List<HttpRequestMessage> Requests = new();
        public readonly List<string> Bodies = new();

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            // Body disalin sekarang: request di-dispose provider setelah kirim.
            Bodies.Add(request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync());
            return _responder(request);
        }
    }

    /// <summary>Stub IAppSettingsService — hanya expiry yang dipakai provider.</summary>
    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public int ExpiryMinutes { get; set; } = 30;

        public Task<string?> GetAsync(string key, string? defaultValue = null) =>
            Task.FromResult(defaultValue);

        public Task SetAsync(string key, string value) => Task.CompletedTask;

        public Task<int> GetMidtransExpiryMinutesAsync() => Task.FromResult(ExpiryMinutes);

        public Task<bool> IsMaintenanceEnabledAsync() => Task.FromResult(false);

        public Task<MaintenanceConfig> GetMaintenanceConfigAsync() =>
            Task.FromResult(new MaintenanceConfig(false, "", "", null, true));

        public Task UpdateMaintenanceConfigAsync(MaintenanceConfig config) => Task.CompletedTask;
    }

    private static (MidtransProvider Provider, FakeHttpMessageHandler Handler) CreateProvider(
        Func<HttpRequestMessage, HttpResponseMessage> responder, int expiryMinutes = 30)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var settings = new MidtransSettings
        {
            ServerKey = ServerKey,
            CoreApiBaseUrl = CoreApiBaseUrl,
            FrontendBaseUrl = "http://localhost:5173"
        };
        return (new MidtransProvider(
            new HttpClient(handler),
            Options.Create(settings),
            new StubAppSettingsService { ExpiryMinutes = expiryMinutes }), handler);
    }

    private static HttpResponseMessage Json(int status, string json) =>
        new((System.Net.HttpStatusCode)status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string ExpectedAuthHeader() =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(ServerKey + ":"));

    // ── CreateChargeAsync ──

    [Fact]
    public async Task CreateChargeAsync_Qris_BuildsChargeRequestAndParsesInstructions()
    {
        var (provider, handler) = CreateProvider(
            r => r.RequestUri!.ToString().EndsWith("/v2/charge")
                ? Json(201, """
                    {
                      "status_code": "201",
                      "status_message": "Success, QRIS transaction is created",
                      "transaction_id": "c1f0a2f5-1e2b-4c3d-9a8b-7c6d5e4f3a2b",
                      "order_id": "INV-20260907-ABCDEF1234",
                      "gross_amount": "15199.00",
                      "payment_type": "qris",
                      "transaction_status": "pending",
                      "fraud_status": "accept",
                      "qr_string": "00020101021226650014COM.MIDTRANS.WWW...",
                      "actions": [
                        { "name": "generate-qr-code", "url": "https://api.sandbox.midtrans.com/v2/qris/abc/qr-code" }
                      ]
                    }
                    """)
                : Json(404, "{}"),
            expiryMinutes: 45);

        var result = await provider.CreateChargeAsync(new MidtransChargeRequest(
            OrderNo, 15199m, "42", "5280 CP", "123456789", "qris"));

        // ── Permintaan: URL, method, auth ──
        var sent = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.sandbox.midtrans.com/v2/charge", sent.RequestUri!.ToString());
        Assert.Equal(ExpectedAuthHeader(), sent.Headers.Authorization!.ToString());

        // ── Payload: payment_type + detail transaksi + expiry dari AppSettings ──
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var root = body.RootElement;
        Assert.Equal("qris", root.GetProperty("payment_type").GetString());
        Assert.Equal(OrderNo, root.GetProperty("transaction_details").GetProperty("order_id").GetString());
        Assert.Equal(15199, root.GetProperty("transaction_details").GetProperty("gross_amount").GetInt32());
        Assert.Equal(15199, root.GetProperty("item_details")[0].GetProperty("price").GetInt32());
        Assert.Equal(1, root.GetProperty("item_details")[0].GetProperty("quantity").GetInt32());
        Assert.Equal("Digitalin 123456789", root.GetProperty("customer_details").GetProperty("first_name").GetString());
        Assert.Equal(45, root.GetProperty("expiry").GetProperty("duration").GetInt32());
        Assert.Equal("minute", root.GetProperty("expiry").GetProperty("unit").GetString());
        // Tanpa Bank → tidak ada objek detail kanal.
        Assert.False(root.TryGetProperty("qris", out _));

        // ── Respons: envelope + instruksi QRIS ──
        Assert.Equal("c1f0a2f5-1e2b-4c3d-9a8b-7c6d5e4f3a2b", result.TransactionId);
        Assert.Equal("qris", result.PaymentType);
        Assert.Equal("pending", result.TransactionStatus);
        Assert.Equal("accept", result.FraudStatus);
        Assert.Equal("201", result.StatusCode);
        Assert.Equal("00020101021226650014COM.MIDTRANS.WWW...", result.QrString);
        var action = Assert.Single(result.Actions);
        Assert.Equal("generate-qr-code", action.Name);
        Assert.Equal("https://api.sandbox.midtrans.com/v2/qris/abc/qr-code", action.Url);
        Assert.Empty(result.VaNumbers);
        Assert.Null(result.PermataVaNumber);
    }

    [Fact]
    public async Task CreateChargeAsync_BankTransfer_IncludesChannelDetailsAndParsesVa()
    {
        var (provider, handler) = CreateProvider(
            r => Json(201, """
                {
                  "status_code": "201",
                  "status_message": "Success, Bank Transfer transaction is created",
                  "transaction_id": "va-tx-1",
                  "order_id": "INV-20260907-ABCDEF1234",
                  "payment_type": "bank_transfer",
                  "transaction_status": "pending",
                  "fraud_status": "accept",
                  "va_numbers": [ { "bank": "bca", "va_number": "91019021581" } ],
                  "permata_va_number": "8708000000000001",
                  "bill_key": "123456789012345678",
                  "biller_code": "70012"
                }
                """));

        var result = await provider.CreateChargeAsync(new MidtransChargeRequest(
            OrderNo, 50000m, "42", "5280 CP", "123456789", "bank_transfer", Bank: "bca"));

        // Detail kanal VA diletakkan di bawah key payment_type-nya.
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal("bca", body.RootElement.GetProperty("bank_transfer").GetProperty("bank").GetString());

        var va = Assert.Single(result.VaNumbers);
        Assert.Equal("bca", va.Bank);
        Assert.Equal("91019021581", va.VaNumber);
        Assert.Equal("8708000000000001", result.PermataVaNumber);
        Assert.Equal("123456789012345678", result.BillKey);
        Assert.Equal("70012", result.BillerCode);
        Assert.Equal("va-tx-1", result.TransactionId);
        Assert.Equal("pending", result.TransactionStatus);
        Assert.Null(result.QrString);
    }

    [Fact]
    public async Task CreateChargeAsync_ShopeePay_IncludesCallbackUrl()
    {
        var (provider, handler) = CreateProvider(
            r => Json(201, """
                {
                  "status_code": "201",
                  "status_message": "Success, ShopeePay transaction is created",
                  "transaction_id": "sp-tx-1",
                  "order_id": "INV-20260907-ABCDEF1234",
                  "payment_type": "shopeepay",
                  "transaction_status": "pending",
                  "fraud_status": "accept",
                  "qr_string": "00020101021226SHOPEEPAY",
                  "actions": [ { "name": "generate-qr-code", "url": "https://api.sandbox.midtrans.com/v2/qris/sp/qr-code" } ]
                }
                """));

        var result = await provider.CreateChargeAsync(new MidtransChargeRequest(
            OrderNo, 15199m, "42", "5280 CP", "123456789", "shopeepay"));

        // ShopeePay mewajibkan callback_url → halaman status FE + orderNo.
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var root = body.RootElement;
        Assert.Equal("shopeepay", root.GetProperty("payment_type").GetString());
        Assert.Equal(
            "http://localhost:5173/order/status/INV-20260907-ABCDEF1234",
            root.GetProperty("shopeepay").GetProperty("callback_url").GetString());

        Assert.Equal("sp-tx-1", result.TransactionId);
        Assert.Equal("00020101021226SHOPEEPAY", result.QrString);
    }

    [Fact]
    public async Task CreateChargeAsync_Gopay_NoChannelDetails()
    {
        var (provider, handler) = CreateProvider(
            r => Json(201, """
                { "status_code": "201", "transaction_id": "gp-tx-1", "payment_type": "gopay", "transaction_status": "pending" }
                """));

        await provider.CreateChargeAsync(new MidtransChargeRequest(
            OrderNo, 15199m, "42", "5280 CP", "123456789", "gopay"));

        // GoPay tidak mewajibkan callback — payload tetap tanpa detail kanal
        // (perilaku lama, jangan regresi).
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.False(body.RootElement.TryGetProperty("gopay", out _));
        Assert.False(body.RootElement.TryGetProperty("shopeepay", out _));
    }

    [Fact]
    public async Task CreateChargeAsync_Mandiri_MapsToEchannelChannel()
    {
        var (provider, handler) = CreateProvider(
            r => Json(201, """
                {
                  "status_code": "201",
                  "status_message": "OK, Mandiri Bill transaction is successful",
                  "transaction_id": "md-tx-1",
                  "order_id": "INV-20260907-ABCDEF1234",
                  "payment_type": "echannel",
                  "transaction_status": "pending",
                  "fraud_status": "accept",
                  "bill_key": "864116861019",
                  "biller_code": "70012"
                }
                """));

        var result = await provider.CreateChargeAsync(new MidtransChargeRequest(
            OrderNo, 15199m, "42", "5280 CP", "123456789", "bank_transfer", Bank: "mandiri"));

        // Payload: payment_type echannel + bill_info — TANPA bank_transfer
        // (bank:"mandiri" di bank_transfer diam-diam menghasilkan VA Permata).
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var root = body.RootElement;
        Assert.Equal("echannel", root.GetProperty("payment_type").GetString());
        Assert.Equal("Digitalin", root.GetProperty("echannel").GetProperty("bill_info1").GetString());
        Assert.Equal(OrderNo, root.GetProperty("echannel").GetProperty("bill_info2").GetString());
        Assert.False(root.TryGetProperty("bank_transfer", out _));

        // Respons: bill_key + biller_code ter-parse (difungsikan instruksi FE).
        Assert.Equal("echannel", result.PaymentType);
        Assert.Equal("864116861019", result.BillKey);
        Assert.Equal("70012", result.BillerCode);
        Assert.Null(result.QrString);
        Assert.Empty(result.VaNumbers);
    }

    [Fact]
    public async Task CreateChargeAsync_Cstore_Indomaret_NoExpiry_WithStoreChannel()
    {
        var (provider, handler) = CreateProvider(
            r => Json(201, """
                {
                  "status_code": "201",
                  "status_message": "Success, cstore transaction is successful",
                  "transaction_id": "cs-tx-1",
                  "order_id": "INV-20260907-ABCDEF1234",
                  "merchant_id": "M241438321",
                  "payment_type": "cstore",
                  "transaction_status": "pending",
                  "fraud_status": "accept",
                  "payment_code": "146028402627",
                  "store": "indomaret"
                }
                """));

        var result = await provider.CreateChargeAsync(new MidtransChargeRequest(
            OrderNo, 15199m, "42", "5280 CP", "123456789", "cstore", Store: "indomaret"));

        // Payload: cstore.store + message ≤20 char, TANPA expiry (Midtrans
        // mengabaikannya — 24 jam fixed; kirim hanya menyesatkan).
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var root = body.RootElement;
        Assert.Equal("cstore", root.GetProperty("payment_type").GetString());
        Assert.Equal("indomaret", root.GetProperty("cstore").GetProperty("store").GetString());
        Assert.Equal("Topup Digitalin", root.GetProperty("cstore").GetProperty("message").GetString());
        Assert.False(root.TryGetProperty("expiry", out _));

        // Respons: payment_code + store + merchant_id ter-parse (merchant id
        // dibutuhkan simulator sandbox Indomaret).
        Assert.Equal("cs-tx-1", result.TransactionId);
        Assert.Equal("cstore", result.PaymentType);
        Assert.Equal("146028402627", result.PaymentCode);
        Assert.Equal("indomaret", result.Store);
        Assert.Equal("M241438321", result.MerchantId);
    }

    [Fact]
    public async Task CreateChargeAsync_Qris_StillSendsExpiry()
    {
        var (provider, handler) = CreateProvider(
            r => Json(201, """{ "status_code": "201", "payment_type": "qris", "transaction_status": "pending", "qr_string": "x" }"""));

        await provider.CreateChargeAsync(new MidtransChargeRequest(
            OrderNo, 15199m, "42", "5280 CP", "123456789", "qris"));

        // Regresi: metode non-cstore tetap mengirim expiry (dari AppSettings).
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.True(body.RootElement.TryGetProperty("expiry", out var expiry));
        Assert.Equal(30, expiry.GetProperty("duration").GetInt32());
    }

    [Fact]
    public async Task CreateChargeAsync_BcaVa_UsesBankTransferChannel()
    {
        var (provider, handler) = CreateProvider(
            r => Json(201, """
                { "status_code": "201", "payment_type": "bank_transfer", "transaction_status": "pending",
                  "va_numbers": [ { "bank": "bca", "va_number": "91019021581" } ] }
                """));

        await provider.CreateChargeAsync(new MidtransChargeRequest(
            OrderNo, 15199m, "42", "5280 CP", "123456789", "bank_transfer", Bank: "bca"));

        // Regresi: VA biasa tetap lewat bank_transfer.bank.
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var root = body.RootElement;
        Assert.Equal("bank_transfer", root.GetProperty("payment_type").GetString());
        Assert.Equal("bca", root.GetProperty("bank_transfer").GetProperty("bank").GetString());
        Assert.False(root.TryGetProperty("echannel", out _));
    }

    [Fact]
    public async Task CreateChargeAsync_HttpError_ThrowsWithResponseBody()
    {
        var (provider, _) = CreateProvider(
            r => Json(400, """{"status_code":"400","status_message":"unsupported payment_type"}"""));

        var ex = await Assert.ThrowsAsync<Exception>(
            () => provider.CreateChargeAsync(new MidtransChargeRequest(
                OrderNo, 15199m, "42", "5280 CP", "123456789", "metode-tak-dikenal")));

        Assert.Contains("Gagal membuat transaksi Midtrans Core API", ex.Message);
        Assert.Contains("unsupported payment_type", ex.Message);
    }

    // ── GetStatusAsync ──

    [Fact]
    public async Task GetStatusAsync_Found_ParsesStatus()
    {
        var (provider, handler) = CreateProvider(
            r => Json(200, """
                {
                  "status_code": "200",
                  "status_message": "Success, transaction found",
                  "transaction_id": "tx-status-1",
                  "order_id": "INV-20260907-ABCDEF1234",
                  "payment_type": "qris",
                  "transaction_status": "settlement",
                  "fraud_status": "accept",
                  "gross_amount": "15199.00",
                  "settlement_time": "2026-09-07 10:05:00"
                }
                """));

        var status = await provider.GetStatusAsync(OrderNo);

        var sent = handler.Requests.Single();
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://api.sandbox.midtrans.com/v2/INV-20260907-ABCDEF1234/status",
            sent.RequestUri!.ToString());
        Assert.Equal(ExpectedAuthHeader(), sent.Headers.Authorization!.ToString());

        Assert.NotNull(status);
        Assert.Equal("tx-status-1", status!.TransactionId);
        Assert.Equal(OrderNo, status.OrderId);
        Assert.Equal("qris", status.PaymentType);
        Assert.Equal("settlement", status.TransactionStatus);
        Assert.Equal("accept", status.FraudStatus);
        Assert.Equal("15199.00", status.GrossAmount);
        Assert.Equal("2026-09-07 10:05:00", status.SettlementTime);
    }

    [Fact]
    public async Task GetStatusAsync_NotFound_ReturnsNull()
    {
        var (provider, handler) = CreateProvider(
            r => Json(404, """{"status_code":"404","status_message":"The requested resource is not found"}"""));

        var status = await provider.GetStatusAsync(OrderNo);

        Assert.Null(status);
        Assert.Single(handler.Requests);
    }

    // ── CancelAsync / ExpireAsync ──

    [Fact]
    public async Task CancelAsync_Accepted_PostsToCancelEndpoint_ReturnsTrue()
    {
        var (provider, handler) = CreateProvider(
            r => Json(200, """{"status_code":"200","status_message":"Success, transaction is canceled","transaction_status":"cancel"}"""));

        var cancelled = await provider.CancelAsync(OrderNo);

        Assert.True(cancelled);
        var sent = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.sandbox.midtrans.com/v2/INV-20260907-ABCDEF1234/cancel",
            sent.RequestUri!.ToString());
        Assert.Equal(ExpectedAuthHeader(), sent.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task CancelAsync_RejectedByMidtrans_ReturnsFalse()
    {
        // 412: transaksi sudah settlement — modifier tidak diizinkan.
        var (provider, _) = CreateProvider(
            r => Json(412, """{"status_code":"412","status_message":"Modifier is not allowed"}"""));

        Assert.False(await provider.CancelAsync(OrderNo));
    }

    [Fact]
    public async Task ExpireAsync_Accepted_PostsToExpireEndpoint_ReturnsTrue()
    {
        var (provider, handler) = CreateProvider(
            r => Json(201, """{"status_code":"201","status_message":"Success, transaction is expired","transaction_status":"expire"}"""));

        var expired = await provider.ExpireAsync(OrderNo);

        Assert.True(expired);
        Assert.Equal("https://api.sandbox.midtrans.com/v2/INV-20260907-ABCDEF1234/expire",
            handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task ExpireAsync_ServerError_Throws()
    {
        var (provider, _) = CreateProvider(
            r => Json(500, """{"status_code":"500","status_message":"Internal server error"}"""));

        var ex = await Assert.ThrowsAsync<Exception>(() => provider.ExpireAsync(OrderNo));

        Assert.Contains("expire", ex.Message);
    }

    // ── Kontrak konfigurasi (fail-fast saat boot via ValidateOnStart) ──

    [Fact]
    public void MidtransSettings_MissingCoreApiBaseUrl_FailsValidation()
    {
        var settings = new MidtransSettings { ServerKey = "key" };
        var context = new ValidationContext(settings);
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(settings, context, results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains("CoreApiBaseUrl"));
    }
}
