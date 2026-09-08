using System.Security.Cryptography;
using System.Text;
using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.DTOs.Responses;
using be_digitalin.Services.Commerce;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using VoucherEntity = be_digitalin.Domain.Entities.Voucher;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Matriks test webhook Midtrans (OrderService.HandleNotificationAsync) —
/// jalur uang lengkap dari audit §11. Sqlite in-memory dipakai karena jalur
/// settlement membuka DB transaction (InMemory tidak mendukung).
///
/// Default StubMidtransProvider.GetStatusAsync = null (404) → fail-open:
/// semua test routing mereproduksi perilaku pra-re-check; test re-check
/// khusus mengatur StatusResult/StatusException.
/// </summary>
public class OrderServiceNotificationTests
{
    private const string ServerKey = "SB-Mid-test-server-key";
    private const string OrderNo = "INV-20260907-ABCDEF1234";
    private const decimal Amount = 15199m;

    // ── Stub ─────────────────────────────────────────────────────────

    private sealed class StubMidtransProvider : IMidtransProvider
    {
        public MidtransTransactionStatus? StatusResult { get; set; }
        public Exception? StatusException { get; set; }

        public Task<(string? Token, string? RedirectUrl)> CreateSnapTransactionAsync(
            string orderNo, decimal grossAmount, string productId, string productName,
            string customerName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MidtransChargeResult> CreateChargeAsync(
            MidtransChargeRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> CancelAsync(string orderNo, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExpireAsync(string orderNo, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public bool ValidateSignature(string orderId, string statusCode, string grossAmount, string signatureKey)
        {
            if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(statusCode) ||
                string.IsNullOrEmpty(grossAmount) || string.IsNullOrEmpty(signatureKey))
                return false;

            var expected = Sha512Hex(orderId + statusCode + grossAmount + ServerKey);
            return string.Equals(expected, signatureKey, StringComparison.Ordinal);
        }

        public Task<MidtransTransactionStatus?> GetStatusAsync(
            string orderNo, CancellationToken cancellationToken = default)
        {
            if (StatusException != null) throw StatusException;
            return Task.FromResult(StatusResult);
        }
    }

    private sealed class StubDigiflazzProvider : IDigiflazzProvider
    {
        public int TopupCalls { get; private set; }
        public string? SnResult { get; set; }
        public Exception? TopupException { get; set; }

        public Task<string?> TopupAsync(
            string buyerSkuCode, string customerNo, string refId,
            CancellationToken cancellationToken = default)
        {
            TopupCalls++;
            if (TopupException != null) throw TopupException;
            return Task.FromResult(SnResult);
        }

        public Task<(bool IsValid, string? Username, string Message)> CheckUsernameAsync(
            string buyerSkuCode, string customerNo, string refId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((false, (string?)null, ""));

        public Task<List<DigiflazzPriceItem>> GetPriceListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<DigiflazzPriceItem>());

        public Task<decimal> GetBalanceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0m);

        public bool ValidateWebhookSignature(string rawBody, string signatureHeader) => false;

        public Task<(string Status, string Sn, string Message)> CheckStatusAsync(
            string sku, string customerNo, string refId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(("", "", ""));
    }

    private sealed class StubTelegramProvider : ITelegramProvider
    {
        public Task SendNotificationAsync(string message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubVoucherService : IVoucherService
    {
        public List<string> ReleasedVouchers { get; } = [];

        public Task<(VoucherEntity? Voucher, string? Error)> ValidateVoucherAsync(
            string code, decimal transactionAmount) =>
            Task.FromResult<(VoucherEntity?, string?)>((null, null));

        public decimal CalculateDiscount(VoucherEntity voucher, decimal productPrice) => 0m;

        public Task ReserveVoucherAsync(VoucherEntity voucher) => Task.CompletedTask;

        public Task ReleaseVoucherAsync(string voucherCode)
        {
            ReleasedVouchers.Add(voucherCode);
            return Task.CompletedTask;
        }

        // Admin CRUD — tidak dipakai jalur webhook.
        public Task<AdminVoucherListResponse> GetVouchersAsync(
            string? search, string? status, string? sortBy, string? sortOrder, int page, int limit) =>
            throw new NotSupportedException();
        public Task<AdminVoucherItem?> GetVoucherByIdAsync(int id) => throw new NotSupportedException();
        public Task<AdminVoucherItem> CreateVoucherAsync(CreateVoucherRequest request) => throw new NotSupportedException();
        public Task<AdminVoucherItem?> UpdateVoucherAsync(int id, UpdateVoucherRequest request) => throw new NotSupportedException();
        public Task<bool?> UpdateVoucherStatusAsync(int id, UpdateVoucherStatusRequest request) => throw new NotSupportedException();
        public Task<(bool Success, string? ErrorMessage)> DeleteVoucherAsync(int id) => throw new NotSupportedException();
        public Task<VoucherUsageResponse> GetVoucherUsageAsync(int id, int page, int limit) => throw new NotSupportedException();
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public Task<string?> GetAsync(string key, string? defaultValue = null) => Task.FromResult(defaultValue);
        public Task SetAsync(string key, string value) => Task.CompletedTask;
        public Task<int> GetMidtransExpiryMinutesAsync() =>
            Task.FromResult(AppConstants.SystemSettings.MidtransExpiryDefaultMinutes);
        public Task<bool> IsMaintenanceEnabledAsync() => Task.FromResult(false);
        public Task<MaintenanceConfig> GetMaintenanceConfigAsync() =>
            Task.FromResult(new MaintenanceConfig(false, "", "", null, true));
        public Task UpdateMaintenanceConfigAsync(MaintenanceConfig config) => Task.CompletedTask;
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static string Sha512Hex(string input) =>
        Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(input))).ToLower();

    /// <summary>
    /// DB Sqlite in-memory untuk satu test: koneksi WAJIB tetap terbuka
    /// selama context hidup (in-memory mati saat koneksi ditutup), maka
    /// keduanya dibungkus satu IAsyncDisposable.
    /// </summary>
    private sealed class NotificationTestDb : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public DigitalinDbContext Db { get; }

        private NotificationTestDb(SqliteConnection connection, DigitalinDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<NotificationTestDb> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var db = new DigitalinDbContext(
                new DbContextOptionsBuilder<DigitalinDbContext>()
                    .UseSqlite(connection)
                    .Options);
            await db.Database.EnsureCreatedAsync();
            db.GameProducts.Add(new GameProduct
            {
                Name = "5280 CP",
                GameCode = "TESTGAME",
                BuyerSkuCode = "sku-1",
                Brand = "TESTGAME",
                Category = "Games",
                Price = Amount,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return new NotificationTestDb(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private static TopupOrder NewOrder(
        string status = AppConstants.OrderStatus.Pending,
        string? voucherCode = null,
        string? paymentType = null,
        string? midtransTransactionId = null) =>
        new()
        {
            OrderNo = OrderNo,
            PlayerId = "123456789",
            Status = status,
            OrderDate = DateTime.UtcNow,
            GameProductId = 1,
            VoucherCode = voucherCode,
            ProductName = "5280 CP",
            BuyerSkuCode = "sku-1",
            BrandName = "TESTGAME",
            CategoryName = "Games",
            OriginalPrice = Amount,
            TotalPaid = Amount,
            CostPrice = 10000m,
            ProfitAmount = 5199m,
            PaymentType = paymentType,
            MidtransTransactionId = midtransTransactionId
        };

    private static MidtransNotificationRequest Notification(
        string transactionStatus,
        string? fraudStatus = "accept",
        string? grossAmount = "15199.00",
        string? orderNo = OrderNo,
        string? paymentType = null,
        string? transactionId = null,
        bool validSignature = true,
        string statusCode = "200")
    {
        var signature = Sha512Hex(orderNo + statusCode + grossAmount + ServerKey);
        return new MidtransNotificationRequest(
            orderNo, statusCode, grossAmount, transactionStatus,
            validSignature ? signature : "deadbeef",
            fraud_status: fraudStatus,
            payment_type: paymentType,
            transaction_id: transactionId);
    }

    private static OrderService CreateService(
        DigitalinDbContext db,
        StubMidtransProvider? midtrans = null,
        StubDigiflazzProvider? digiflazz = null,
        StubVoucherService? voucher = null) =>
        new(db,
            midtrans ?? new StubMidtransProvider(),
            digiflazz ?? new StubDigiflazzProvider(),
            new StubTelegramProvider(),
            voucher ?? new StubVoucherService(),
            new StubAppSettingsService(),
            NullLogger<OrderService>.Instance);

    private static async Task<TopupOrder> ReloadAsync(DigitalinDbContext db)
    {
        var order = await db.TopupOrders.AsNoTracking()
            .SingleAsync(o => o.OrderNo == OrderNo);
        return order;
    }

    // ── Signature ────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidSignature_ReturnsFalse_OrderUntouched()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement", validSignature: false));

        Assert.False(result); // → 401 di controller
        Assert.Equal(AppConstants.OrderStatus.Pending, (await ReloadAsync(db)).Status);
        Assert.Equal(0, digiflazz.TopupCalls);
    }

    // ── Order lookup ─────────────────────────────────────────────────

    [Fact]
    public async Task OrderNotFound_AcksWithoutSideEffects()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement", orderNo: "INV-TIDAK-ADA"));

        Assert.True(result); // ack 200 — jangan minta Midtrans retry
        Assert.Equal(0, digiflazz.TopupCalls);
    }

    // ── Duplicate / late notification ────────────────────────────────

    [Fact]
    public async Task AlreadyPaid_DuplicateSettlement_Ignored_NoDoubleFulfillment()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder(status: AppConstants.OrderStatus.Paid));
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Paid, (await ReloadAsync(db)).Status);
        Assert.Equal(0, digiflazz.TopupCalls);
    }

    // ── Routing status non-settlement ────────────────────────────────

    [Fact]
    public async Task PendingStatus_NoOp()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var result = await CreateService(db).HandleNotificationAsync(Notification("pending"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Pending, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task UnknownStatus_NoOp()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var result = await CreateService(db).HandleNotificationAsync(Notification("foo-status"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Pending, (await ReloadAsync(db)).Status);
    }

    [Theory]
    [InlineData("expire", AppConstants.OrderStatus.Expired)]
    [InlineData("cancel", AppConstants.OrderStatus.Failed)]
    [InlineData("deny", AppConstants.OrderStatus.Failed)]
    public async Task NegativeStatus_MarksTerminal_AndReleasesVoucher(string status, string expected)
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder(voucherCode: "HEMAT10"));
        await db.SaveChangesAsync();
        var voucher = new StubVoucherService();

        var result = await CreateService(db, voucher: voucher)
            .HandleNotificationAsync(Notification(status));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal(expected, order.Status);
        Assert.Contains("HEMAT10", voucher.ReleasedVouchers);
    }

    // ── Guard fraud + nominal (notifikasi) ───────────────────────────

    [Fact]
    public async Task Settlement_FraudChallenge_NoOp()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement", fraudStatus: "challenge"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Pending, (await ReloadAsync(db)).Status);
        Assert.Equal(0, digiflazz.TopupCalls);
    }

    // ── fraud_status tidak selalu dikirim (settlement cstore, verifikasi
    //    sandbox E2E 2026-09-08) — null tidak boleh menolak pembayaran ──

    [Fact]
    public async Task Settlement_WithoutFraudStatus_FailOpen_Paid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement", fraudStatus: null));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Processing, (await ReloadAsync(db)).Status);
        Assert.Equal(1, digiflazz.TopupCalls);
    }

    [Fact]
    public async Task Settlement_WithoutFraudStatus_StatusApiDeny_NotPaid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        // fraud_status null di notifikasi — konfirmasi status API (yang selalu
        // memuat fraud_status) yang harus menahan transisi PAID.
        var midtrans = new StubMidtransProvider { StatusResult = StatusOf("settlement", fraudStatus: "deny") };
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, midtrans: midtrans, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement", fraudStatus: null));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Pending, (await ReloadAsync(db)).Status);
        Assert.Equal(0, digiflazz.TopupCalls);
    }

    [Fact]
    public async Task Settlement_WithoutFraudStatus_StatusApiAccept_Paid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var midtrans = new StubMidtransProvider { StatusResult = StatusOf("settlement", fraudStatus: "accept") };

        var result = await CreateService(db, midtrans: midtrans)
            .HandleNotificationAsync(Notification("settlement", fraudStatus: null));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Processing, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task Settlement_WrongAmount_NoOp()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider();

        // Signature valid untuk nominal SALAH — guard nominal yang harus menolak.
        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement", grossAmount: "999.00"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Pending, (await ReloadAsync(db)).Status);
        Assert.Equal(0, digiflazz.TopupCalls);
    }

    // ── Settlement / capture — happy path ────────────────────────────

    [Fact]
    public async Task Settlement_Valid_MarksPaidAndFulfills()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Processing, order.Status); // tanpa SN instan
        Assert.NotNull(order.ProcessedAt);
        Assert.Equal(1, digiflazz.TopupCalls);
    }

    [Fact]
    public async Task Settlement_Valid_InstantSn_Completed()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider { SnResult = "SN-INSTAN-1" };

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Completed, order.Status);
        Assert.Equal("SN-INSTAN-1", order.Sn);
        Assert.Equal(1, digiflazz.TopupCalls);
    }

    [Fact]
    public async Task Capture_Valid_MarksPaid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider { SnResult = "SN-CC-1" };

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("capture"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Completed, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task Settlement_FulfillmentFails_StaysPaidFamily_NeedsAction()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var digiflazz = new StubDigiflazzProvider
        {
            TopupException = new Exception("Saldo supplier habis")
        };

        var result = await CreateService(db, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, order.Status);
        Assert.Equal(1, digiflazz.TopupCalls);
        Assert.NotNull(order.ProcessedAt); // PAID pernah ter-commit
    }

    // ── Re-check status (defense-in-depth Phase 3) ───────────────────

    private static MidtransTransactionStatus StatusOf(
        string transactionStatus,
        string? fraudStatus = "accept",
        string? grossAmount = "15199.00",
        string? transactionId = "tx-status-1") =>
        new(transactionId, OrderNo, "qris", transactionStatus, fraudStatus, "200", grossAmount, null);

    [Fact]
    public async Task Settlement_StatusApiConfirms_MarksPaid_AndRecordsTransactionId()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var midtrans = new StubMidtransProvider
        {
            StatusResult = StatusOf("settlement", transactionId: "tx-status-9")
        };

        var result = await CreateService(db, midtrans: midtrans)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Processing, order.Status);
        Assert.Equal("tx-status-9", order.MidtransTransactionId); // dari status API
    }

    [Theory]
    [InlineData("deny")]
    [InlineData("cancel")]
    [InlineData("expire")]
    public async Task Settlement_StatusApiNegativeContradiction_NotPaid(string apiStatus)
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var midtrans = new StubMidtransProvider { StatusResult = StatusOf(apiStatus) };
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, midtrans: midtrans, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Pending, order.Status); // tidak PAID
        Assert.Equal(0, digiflazz.TopupCalls);
    }

    [Fact]
    public async Task Settlement_StatusApiAmountMismatch_NotPaid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var midtrans = new StubMidtransProvider { StatusResult = StatusOf("settlement", grossAmount: "999.00") };
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, midtrans: midtrans, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Pending, (await ReloadAsync(db)).Status);
        Assert.Equal(0, digiflazz.TopupCalls);
    }

    [Fact]
    public async Task Settlement_StatusApiFraudDenied_NotPaid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var midtrans = new StubMidtransProvider { StatusResult = StatusOf("settlement", fraudStatus: "deny") };
        var digiflazz = new StubDigiflazzProvider();

        var result = await CreateService(db, midtrans: midtrans, digiflazz: digiflazz)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Pending, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task Settlement_StatusApiPendingSnapshot_ProceedsPaid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        // Snapshot "pending" bukan kontradiksi definitif — notifikasi event
        // bisa lebih baru dari snapshot; bayar tetap diproses.
        var midtrans = new StubMidtransProvider { StatusResult = StatusOf("pending") };

        var result = await CreateService(db, midtrans: midtrans)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Processing, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task Settlement_StatusApiThrows_FailOpen_Paid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var midtrans = new StubMidtransProvider
        {
            StatusException = new Exception("Midtrans status API down")
        };

        var result = await CreateService(db, midtrans: midtrans)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Processing, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task Settlement_StatusApi404_FailOpen_Paid()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();
        var midtrans = new StubMidtransProvider { StatusResult = null }; // 404

        var result = await CreateService(db, midtrans: midtrans)
            .HandleNotificationAsync(Notification("settlement"));

        Assert.True(result);
        Assert.Equal(AppConstants.OrderStatus.Processing, (await ReloadAsync(db)).Status);
    }

    // ── Enrichment payment_type / transaction_id ─────────────────────

    [Fact]
    public async Task Settlement_NotificationPaymentTypeAndTransactionId_Persisted()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder()); // order Snap: keduanya null
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .HandleNotificationAsync(Notification(
                "settlement", paymentType: "gopay", transactionId: "tx-notif-5"));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal("gopay", order.PaymentType);
        Assert.Equal("tx-notif-5", order.MidtransTransactionId);
    }

    [Fact]
    public async Task Settlement_CoreOrder_PaymentTypeNotOverwritten()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder(paymentType: "qris", midtransTransactionId: "tx-charge-1"));
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .HandleNotificationAsync(Notification(
                "settlement", paymentType: "gopay", transactionId: "tx-notif-9"));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal("qris", order.PaymentType); // nilai charge dipertahankan
        Assert.Equal("tx-charge-1", order.MidtransTransactionId);
    }

    [Fact]
    public async Task Expire_NotificationPaymentType_Persisted()
    {
        await using var testDb = await NotificationTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .HandleNotificationAsync(Notification("expire", paymentType: "bank_transfer"));

        Assert.True(result);
        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Expired, order.Status);
        Assert.Equal("bank_transfer", order.PaymentType);
    }
}
