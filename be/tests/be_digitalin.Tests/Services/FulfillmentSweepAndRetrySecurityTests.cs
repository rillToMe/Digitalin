using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using be_digitalin.Application.Ports;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Orders;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.DTOs.Responses;
using be_digitalin.Providers;
using be_digitalin.Services.Commerce;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using VoucherEntity = be_digitalin.Domain.Entities.Voucher;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Regression tests untuk Phase 2A & Phase 2A Follow-up:
/// - CRIT-02: Rekonsiliasi status Midtrans, ExpireAsync pada SweepStuckOrdersAsync,
///   serta atomisitas dan integritas kepemilikan reservasi kuota voucher pada late payment / refund.
/// - MED-03: Klasifikasi eksplisit respons Digiflazz, pencegahan pengiriman ganda CheckStatusAsync + TopupAsync,
///   single-submission retry untuk order NeverSubmitted, dan fail-closed untuk UncertainOutcome.
/// </summary>
public class FulfillmentSweepAndRetrySecurityTests
{
    private const string ServerKey = "SB-Mid-test-server-key";
    private const string OrderNo = "INV-20261027-SWEEP0001";
    private const decimal Amount = 15199m;

    private sealed class StubMidtransProvider : IMidtransProvider
    {
        public MidtransTransactionStatus? StatusResult { get; set; }
        public Exception? StatusException { get; set; }
        public bool ExpireResult { get; set; } = true;
        public Exception? ExpireException { get; set; }
        public int GetStatusCalls { get; private set; }
        public int ExpireCalls { get; private set; }

        public Task<(string? Token, string? RedirectUrl)> CreateSnapTransactionAsync(
            string orderNo, decimal grossAmount, string productId, string productName,
            string customerName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MidtransChargeResult> CreateChargeAsync(
            MidtransChargeRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> CancelAsync(string orderNo, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExpireAsync(string orderNo, CancellationToken cancellationToken = default)
        {
            ExpireCalls++;
            if (ExpireException != null) throw ExpireException;
            return Task.FromResult(ExpireResult);
        }

        public bool ValidateSignature(string orderId, string statusCode, string grossAmount, string signatureKey)
        {
            if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(statusCode) ||
                string.IsNullOrEmpty(grossAmount) || string.IsNullOrEmpty(signatureKey))
                return false;

            var expected = Convert.ToHexString(
                SHA512.HashData(Encoding.UTF8.GetBytes(orderId + statusCode + grossAmount + ServerKey))).ToLowerInvariant();
            return string.Equals(expected, signatureKey, StringComparison.Ordinal);
        }

        public Task<MidtransTransactionStatus?> GetStatusAsync(
            string orderNo, CancellationToken cancellationToken = default)
        {
            GetStatusCalls++;
            if (StatusException != null) throw StatusException;
            return Task.FromResult(StatusResult);
        }
    }

    private sealed class StubDigiflazzProvider : IDigiflazzProvider
    {
        private int _topupCalls;
        private int _checkStatusCalls;

        public int TopupCalls => _topupCalls;
        public string? LastTopupRefId { get; private set; }
        public string? SnResult { get; set; } = string.Empty;
        public Exception? TopupException { get; set; }

        public int CheckStatusCalls => _checkStatusCalls;
        public (string Status, string Sn, string Message) CheckStatusResult { get; set; } =
            (AppConstants.DigiflazzStatus.Gagal, "", "Transaksi gagal");
        public Exception? CheckStatusException { get; set; }

        public Task<string?> TopupAsync(
            string buyerSkuCode, string customerNo, string refId,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _topupCalls);
            LastTopupRefId = refId;
            if (TopupException != null) throw TopupException;
            return Task.FromResult(SnResult);
        }

        public Task<(string Status, string Sn, string Message)> CheckStatusAsync(
            string sku, string customerNo, string refId,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _checkStatusCalls);
            if (CheckStatusException != null) throw CheckStatusException;
            return Task.FromResult(CheckStatusResult);
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
    }

    private sealed class StubTelegramProvider : ITelegramProvider
    {
        public List<string> SentMessages { get; } = [];

        public Task SendNotificationAsync(string message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class StubVoucherService : IVoucherService
    {
        public List<string> ReleasedVouchers { get; } = [];
        public Exception? ReleaseException { get; set; }

        public Task<(VoucherEntity? Voucher, string? Error)> ValidateVoucherAsync(
            string code, decimal transactionAmount) =>
            Task.FromResult<(VoucherEntity?, string?)>((null, null));

        public decimal CalculateDiscount(VoucherEntity voucher, decimal productPrice) => 0m;
        public Task ReserveVoucherAsync(VoucherEntity voucher) => Task.CompletedTask;

        public Task ReleaseVoucherAsync(string voucherCode)
        {
            if (ReleaseException != null) throw ReleaseException;
            ReleasedVouchers.Add(voucherCode);
            return Task.CompletedTask;
        }

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
        public Task<int> GetMidtransExpiryMinutesAsync() => Task.FromResult(30);
        public Task<bool> IsMaintenanceEnabledAsync() => Task.FromResult(false);
        public Task<MaintenanceConfig> GetMaintenanceConfigAsync() =>
            Task.FromResult(new MaintenanceConfig(false, "", "", null, true));
        public Task UpdateMaintenanceConfigAsync(MaintenanceConfig config) => Task.CompletedTask;
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class SweepTestDb : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public DigitalinDbContext Db { get; }

        private SweepTestDb(SqliteConnection connection, DigitalinDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<SweepTestDb> CreateAsync()
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
            return new SweepTestDb(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private static TopupOrder NewStuckOrder(
        string status,
        string? voucherCode = null,
        DateTime? orderDate = null,
        DateTime? processedAt = null,
        string orderNo = OrderNo) =>
        new()
        {
            OrderNo = orderNo,
            PlayerId = "123456789",
            Status = status,
            OrderDate = orderDate ?? DateTime.UtcNow.AddMinutes(-45), // melewati 30+5 menit cutoff
            ProcessedAt = processedAt,
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
            PaymentType = "qris"
        };

    private static OrderService CreateService(
        DigitalinDbContext db,
        IMidtransProvider? midtrans = null,
        IDigiflazzProvider? digiflazz = null,
        IVoucherService? voucher = null,
        StubTelegramProvider? telegram = null) =>
        new(db,
            midtrans ?? new StubMidtransProvider(),
            digiflazz ?? new StubDigiflazzProvider(),
            telegram ?? new StubTelegramProvider(),
            voucher ?? new StubVoucherService(),
            new StubAppSettingsService(),
            NullLogger<OrderService>.Instance,
            Options.Create(new OrderPublicHashSettings
            {
                SecretKey = "unit-test-hmac-secret-0123456789abcdef"
            }));

    private static MidtransNotificationRequest CreateSignedNotification(
        string orderNo,
        string transactionStatus,
        string grossAmount = "15199.00",
        string statusCode = "200",
        string? fraudStatus = "accept",
        string? transactionId = "tx-1")
    {
        var sig = Convert.ToHexString(
            SHA512.HashData(Encoding.UTF8.GetBytes(orderNo + statusCode + grossAmount + ServerKey))).ToLowerInvariant();
        return new MidtransNotificationRequest(
            orderNo,
            statusCode,
            grossAmount,
            transactionStatus,
            sig,
            fraud_status: fraudStatus,
            payment_type: "qris",
            transaction_id: transactionId);
    }

    private static DigiflazzProvider CreateRealDigiflazzProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.digiflazz.com")
        };
        var settings = Options.Create(new DigiflazzSettings
        {
            Username = "test-user",
            ApiKey = "test-api-key",
            WebhookSecret = "test-webhook-secret",
            BaseUrl = "https://api.digiflazz.com"
        });
        return new DigiflazzProvider(httpClient, settings, NullLogger<DigiflazzProvider>.Instance);
    }

    private static Task<TopupOrder> ReloadAsync(DigitalinDbContext db, string orderNo = OrderNo) =>
        db.TopupOrders.AsNoTracking().SingleAsync(o => o.OrderNo == orderNo);

    // ── CRIT-02: SweepStuckOrdersAsync Midtrans Reconciliation ────────

    [Fact]
    public async Task Sweep_PendingOrder_WhenMidtransSettled_MarksPaidAndFulfills_WithoutReleasingVoucher()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending, voucherCode: "HEMAT10"));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-settled-1", OrderNo, "qris", "settlement", "accept", "200", "15199.00", "2026-10-27 10:00:00")
        };
        var digiflazz = new StubDigiflazzProvider { SnResult = "SN-SWEEP-OK" };
        var voucher = new StubVoucherService();

        await CreateService(db, midtrans: midtrans, digiflazz: digiflazz, voucher: voucher)
            .SweepStuckOrdersAsync();

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Completed, order.Status);
        Assert.Equal("SN-SWEEP-OK", order.Sn);
        Assert.Equal("tx-settled-1", order.MidtransTransactionId);
        Assert.Equal(1, digiflazz.TopupCalls);
        Assert.Equal(0, midtrans.ExpireCalls);
        Assert.Empty(voucher.ReleasedVouchers);
    }

    [Fact]
    public async Task Sweep_PendingOrder_WhenMidtransStillPending_CallsExpireAsync_MarksExpired_AndReleasesVoucher()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending, voucherCode: "HEMAT10"));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-pending-1", OrderNo, "qris", "pending", "accept", "201", "15199.00", null),
            ExpireResult = true
        };
        var digiflazz = new StubDigiflazzProvider();
        var voucher = new StubVoucherService();

        await CreateService(db, midtrans: midtrans, digiflazz: digiflazz, voucher: voucher)
            .SweepStuckOrdersAsync();

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Expired, order.Status);
        Assert.Equal(1, midtrans.ExpireCalls);
        Assert.Equal(0, digiflazz.TopupCalls);
        Assert.Single(voucher.ReleasedVouchers, "HEMAT10");
    }

    [Fact]
    public async Task Sweep_PendingOrder_WhenMidtransGetStatusThrows_KeepsPending_AndDoesNotReleaseVoucher()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending, voucherCode: "HEMAT10"));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusException = new HttpRequestException("Midtrans network unreachable")
        };
        var voucher = new StubVoucherService();

        await CreateService(db, midtrans: midtrans, voucher: voucher)
            .SweepStuckOrdersAsync();

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Pending, order.Status);
        Assert.Equal(0, midtrans.ExpireCalls);
        Assert.Empty(voucher.ReleasedVouchers);
    }

    [Fact]
    public async Task Sweep_PendingOrder_WhenMidtransExpireReturnsFalse_KeepsPending_ForNextCycleReconciliation()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending, voucherCode: "HEMAT10"));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-pending-2", OrderNo, "qris", "pending", "accept", "201", "15199.00", null),
            ExpireResult = false // ditolak Midtrans (mis. 412 karena baru saja settlement)
        };
        var voucher = new StubVoucherService();

        await CreateService(db, midtrans: midtrans, voucher: voucher)
            .SweepStuckOrdersAsync();

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Pending, order.Status);
        Assert.Equal(1, midtrans.ExpireCalls);
        Assert.Empty(voucher.ReleasedVouchers);
    }

    // ── MED-03: RetryOrderAsync & ResumeFulfillmentAsync Fail-Closed ──

    [Fact]
    public async Task RetryOrderAsync_WhenCheckStatusThrowsHttpRequestException_FailsClosed_WithoutCallingTopup()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: DateTime.UtcNow.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            CheckStatusException = new HttpRequestException("Digiflazz gateway error")
        };

        var result = await CreateService(db, digiflazz: digiflazz)
            .RetryOrderAsync(OrderNo);

        Assert.Equal(OrderActionOutcome.Error, result.Outcome);
        Assert.Equal(0, digiflazz.TopupCalls);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task RetryOrderAsync_WhenCheckStatusThrowsTaskCanceledException_FailsClosed_WithoutCallingTopup()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: DateTime.UtcNow.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            CheckStatusException = new TaskCanceledException("Digiflazz check-status timeout")
        };

        var result = await CreateService(db, digiflazz: digiflazz)
            .RetryOrderAsync(OrderNo);

        Assert.Equal(OrderActionOutcome.Error, result.Outcome);
        Assert.Equal(0, digiflazz.TopupCalls);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, (await ReloadAsync(db)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    public async Task RetryOrderAsync_WhenCheckStatusReturnsUnknownStatus_FailsClosed_WithoutCallingTopup(
        string unknownStatus)
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: DateTime.UtcNow.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            CheckStatusResult = (unknownStatus, "", "Response tidak dikenali")
        };

        var result = await CreateService(db, digiflazz: digiflazz)
            .RetryOrderAsync(OrderNo);

        Assert.Equal(OrderActionOutcome.Error, result.Outcome);
        Assert.Equal(0, digiflazz.TopupCalls);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task RetryOrderAsync_WhenCheckStatusReturnsPending_RejectsRetry_WithoutCallingTopup()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: DateTime.UtcNow.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            CheckStatusResult = (AppConstants.DigiflazzStatus.Pending, "", "Masih diproses")
        };

        var result = await CreateService(db, digiflazz: digiflazz)
            .RetryOrderAsync(OrderNo);

        Assert.Equal(OrderActionOutcome.InvalidState, result.Outcome);
        Assert.Equal(0, digiflazz.TopupCalls);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task RetryOrderAsync_WhenCheckStatusReturnsSukses_MarksCompletedWithSn_WithoutCallingTopup()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: DateTime.UtcNow.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            CheckStatusResult = (AppConstants.DigiflazzStatus.Sukses, "SN-EXISTING-99", "Sukses")
        };

        var result = await CreateService(db, digiflazz: digiflazz)
            .RetryOrderAsync(OrderNo);

        Assert.Equal(OrderActionOutcome.Success, result.Outcome);
        Assert.Equal(0, digiflazz.TopupCalls);
        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Completed, order.Status);
        Assert.Equal("SN-EXISTING-99", order.Sn);
    }

    [Fact]
    public async Task RetryOrderAsync_WhenUncertainOrderCheckStatusReturnsGagal_DoesNotCallTopup_AndUnlocksRefund()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: OrderFulfillmentState.CreateUncertainOutcomeTimestamp(DateTime.UtcNow.AddMinutes(-10))));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            CheckStatusResult = (AppConstants.DigiflazzStatus.Gagal, "", "Gagal di sisi supplier"),
            SnResult = "SN-SHOULD-NOT-BE-CALLED"
        };

        var service = CreateService(db, digiflazz: digiflazz);

        // Sebelum rekonsiliasi, Refund harus ditolak karena status supplier masih UncertainOutcome.
        var refundBeforeReconcile = await service.RefundOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.InvalidState, refundBeforeReconcile.Outcome);

        // Saat Retry dipanggil, CheckStatusAsync dipanggil 1x, TopupAsync TIDAK dipanggil (0x),
        // dan status supplier berubah menjadi ConfirmedFailure.
        var retryResult = await service.RetryOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.Error, retryResult.Outcome);
        Assert.Equal(1, digiflazz.CheckStatusCalls);
        Assert.Equal(0, digiflazz.TopupCalls);

        var orderAfterReconcile = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, orderAfterReconcile.Status);
        Assert.Equal(SupplierFulfillmentState.ConfirmedFailure, OrderFulfillmentState.GetSupplierState(orderAfterReconcile));

        // Percobaan Retry berikutnya langsung ditolak tanpa HTTP call karena ref_id sudah ConfirmedFailure.
        var secondRetry = await service.RetryOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.InvalidState, secondRetry.Outcome);
        Assert.Equal(1, digiflazz.CheckStatusCalls);
        Assert.Equal(0, digiflazz.TopupCalls);

        // Sekarang Refund diizinkan karena sudah terkonfirmasi Gagal.
        var refundAfterReconcile = await service.RefundOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.Success, refundAfterReconcile.Outcome);
        Assert.Equal(AppConstants.OrderStatus.Refunded, (await ReloadAsync(db)).Status);
    }

    [Fact]
    public async Task Sweep_ResumeFulfillment_WhenCheckStatusThrows_TransitionsPaidToNeedsAction_Alerts_AndNeverCallsTopup()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.Paid,
            processedAt: DateTime.UtcNow.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            CheckStatusException = new HttpRequestException("Digiflazz check status connection reset")
        };
        var telegram = new StubTelegramProvider();

        await CreateService(db, digiflazz: digiflazz, telegram: telegram)
            .SweepStuckOrdersAsync();

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, order.Status);
        Assert.Equal(SupplierFulfillmentState.UncertainOutcome, OrderFulfillmentState.GetSupplierState(order));
        Assert.Equal(0, digiflazz.TopupCalls);
        Assert.Single(telegram.SentMessages);
    }

    [Fact]
    public async Task Sweep_ResumeFulfillment_WhenCheckStatusReturnsUnknown_TransitionsPaidToNeedsAction_AndNeverCallsTopup()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.Paid,
            processedAt: DateTime.UtcNow.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            CheckStatusResult = ("", "", "Empty status")
        };
        var telegram = new StubTelegramProvider();

        await CreateService(db, digiflazz: digiflazz, telegram: telegram)
            .SweepStuckOrdersAsync();

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, order.Status);
        Assert.Equal(SupplierFulfillmentState.UncertainOutcome, OrderFulfillmentState.GetSupplierState(order));
        Assert.Equal(0, digiflazz.TopupCalls);
        Assert.Single(telegram.SentMessages);
    }

    // ── PHASE E SCENARIO 1: Order A + Order B Last-Slot Voucher Integrity ─

    [Fact]
    public async Task VoucherIntegrity_OrderAExpires_OrderBTakesLastSlot_OrderALatePaidAndRefunded_DoesNotDecrementOrderBReservation()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        var realVoucherService = new VoucherService(db);

        var voucher = new VoucherEntity
        {
            Code = "LASTSLOT1",
            DiscountType = "FixedAmount",
            DiscountValue = 1000m,
            MinimumTransaction = 5000m,
            UsageLimit = 1,
            UsedCount = 0,
            IsActive = true,
            ExpiredAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        const string orderANo = "INV-20261028-ORDERA0001";
        const string orderBNo = "INV-20261028-ORDERB0002";

        // 1. Order A memakai slot terakhir voucher (UsedCount = 1).
        await realVoucherService.ReserveVoucherAsync(voucher);
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.Pending,
            voucherCode: "LASTSLOT1",
            orderDate: DateTime.UtcNow.AddMinutes(-50),
            orderNo: orderANo));
        await db.SaveChangesAsync();
        Assert.Equal(1, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "LASTSLOT1")).UsedCount);

        var midtrans = new StubMidtransProvider();
        var digiflazz = new StubDigiflazzProvider();
        var service = CreateService(db, midtrans: midtrans, digiflazz: digiflazz, voucher: realVoucherService);

        // 2. Order A kedaluwarsa (EXPIRED) dan merilis voucher (UsedCount = 0).
        await service.HandleNotificationAsync(
            CreateSignedNotification(orderANo, "expire", statusCode: "407", transactionId: "tx-late-a"));
        var orderAAfterExpire = await ReloadAsync(db, orderANo);
        Assert.Equal(AppConstants.OrderStatus.Expired, orderAAfterExpire.Status);
        Assert.Null(orderAAfterExpire.ProcessedAt);
        Assert.Equal(0, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "LASTSLOT1")).UsedCount);

        // 3. Order B mengambil slot terakhir voucher tersebut (UsedCount = 1).
        var (validatedForB, validateErrB) = await realVoucherService.ValidateVoucherAsync("LASTSLOT1", Amount);
        Assert.Null(validateErrB);
        Assert.NotNull(validatedForB);
        await realVoucherService.ReserveVoucherAsync(validatedForB!);
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.Pending,
            voucherCode: "LASTSLOT1",
            orderDate: DateTime.UtcNow,
            orderNo: orderBNo));
        await db.SaveChangesAsync();
        Assert.Equal(1, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "LASTSLOT1")).UsedCount);

        // 4. Order A menerima verified late payment -> re-reserve gagal karena kuota habis -> masuk NEEDS_ACTION.
        midtrans.StatusResult = new MidtransTransactionStatus(
            "tx-late-a", orderANo, "qris", "settlement", "accept", "200", "15199.00", null);
        await service.HandleNotificationAsync(
            CreateSignedNotification(orderANo, "settlement", transactionId: "tx-late-a"));

        var orderAAfterLatePayment = await ReloadAsync(db, orderANo);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, orderAAfterLatePayment.Status);
        Assert.Equal("LASTSLOT1", orderAAfterLatePayment.VoucherCode); // jejak audit kode voucher tetap utuh
        Assert.False(OrderFulfillmentState.HasActiveVoucherReservation(orderAAfterLatePayment));
        Assert.Equal(1, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "LASTSLOT1")).UsedCount);

        // 5. Retry Order A ditolak karena kuota voucher tidak ter-reserve.
        var retryOrderA = await service.RetryOrderAsync(orderANo);
        Assert.Equal(OrderActionOutcome.InvalidState, retryOrderA.Outcome);
        Assert.Equal(0, digiflazz.TopupCalls);

        // 6. Admin me-refund Order A -> TIDAK boleh mengurangi UsedCount milik Order B!
        var refundOrderA = await service.RefundOrderAsync(orderANo);
        Assert.Equal(OrderActionOutcome.Success, refundOrderA.Outcome);
        Assert.Equal(AppConstants.OrderStatus.Refunded, (await ReloadAsync(db, orderANo)).Status);
        Assert.Equal(1, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "LASTSLOT1")).UsedCount);

        // 7. Saat Order B kemudian kedaluwarsa, barulah reservasi milik Order B dilepas menjadi 0.
        await service.HandleNotificationAsync(CreateSignedNotification(orderBNo, "expire"));
        Assert.Equal(0, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "LASTSLOT1")).UsedCount);
    }

    // ── PHASE E SCENARIO 2: Late-Paid Re-Reserve + Single Release on Refund ─

    [Fact]
    public async Task VoucherIntegrity_LatePaymentReReservesVoucher_RefundReleasesOnce_SecondRefundDoesNotReleaseAgain()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        var realVoucherService = new VoucherService(db);

        var voucher = new VoucherEntity
        {
            Code = "HEMAT2X",
            DiscountType = "FixedAmount",
            DiscountValue = 1000m,
            MinimumTransaction = 5000m,
            UsageLimit = 2,
            UsedCount = 1, // 1 slot sedang dipakai oleh order lain
            IsActive = true,
            ExpiredAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        };
        db.Vouchers.Add(voucher);
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.Expired,
            voucherCode: "HEMAT2X"));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-late-re", OrderNo, "qris", "settlement", "accept", "200", "15199.00", null)
        };
        var service = CreateService(db, midtrans: midtrans, voucher: realVoucherService);

        // Late payment berhasil melakukan re-reservasi (UsedCount naik dari 1 -> 2).
        await service.HandleNotificationAsync(
            CreateSignedNotification(OrderNo, "settlement", transactionId: "tx-late-re"));

        var orderAfterLate = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, orderAfterLate.Status);
        Assert.True(OrderFulfillmentState.HasActiveVoucherReservation(orderAfterLate));
        Assert.Equal(SupplierFulfillmentState.NeverSubmitted, OrderFulfillmentState.GetSupplierState(orderAfterLate));
        Assert.Equal(2, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "HEMAT2X")).UsedCount);

        // Refund pertama melepaskan reservasi tepat 1 kali (UsedCount turun dari 2 -> 1).
        var firstRefund = await service.RefundOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.Success, firstRefund.Outcome);
        Assert.Equal(1, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "HEMAT2X")).UsedCount);

        // Refund kedua ditolak dan TIDAK mengurangi reservasi milik order lain (UsedCount tetap 1).
        var secondRefund = await service.RefundOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.InvalidState, secondRefund.Outcome);
        Assert.Equal(1, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "HEMAT2X")).UsedCount);
    }

    // ── PHASE E SCENARIO 3: Transaction Rollback Atomicity ───────────

    [Fact]
    public async Task VoucherIntegrity_WhenLatePaymentOrderUpdateFails_VoucherReReservationIsRolledBack()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        var realVoucherService = new VoucherService(db);

        db.Vouchers.Add(new VoucherEntity
        {
            Code = "ROLLBACK1",
            DiscountType = "FixedAmount",
            DiscountValue = 1000m,
            MinimumTransaction = 5000m,
            UsageLimit = 5,
            UsedCount = 0,
            IsActive = true,
            ExpiredAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.Expired,
            voucherCode: "ROLLBACK1"));
        await db.SaveChangesAsync();

        // Pasang trigger SQLite yang menggagalkan UPDATE pada TopupOrders setelah Vouchers di-update.
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TRIGGER abort_topup_order_update
            BEFORE UPDATE ON TopupOrders
            BEGIN
                SELECT RAISE(ABORT, 'Simulated TopupOrders update failure');
            END;");

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-fail-db", OrderNo, "qris", "settlement", "accept", "200", "15199.00", null)
        };
        var service = CreateService(db, midtrans: midtrans, voucher: realVoucherService);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.HandleNotificationAsync(
                CreateSignedNotification(OrderNo, "settlement", transactionId: "tx-fail-db")));

        // Karena berada di dalam transaksi DB yang sama, UsedCount wajib ter-rollback kembali ke 0.
        var voucherAfterFailure = await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "ROLLBACK1");
        Assert.Equal(0, voucherAfterFailure.UsedCount);
        var orderAfterFailure = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Expired, orderAfterFailure.Status);
    }

    [Fact]
    public async Task VoucherIntegrity_WhenVoucherReleaseThrowsDuringRefundOrExpire_OrderStatusChangeIsRolledBack()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            voucherCode: "HEMAT10",
            processedAt: OrderFulfillmentState.CreateNeverSubmittedTimestamp(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var failingVoucherService = new StubVoucherService
        {
            ReleaseException = new InvalidOperationException("Simulated database failure on voucher release")
        };
        var service = CreateService(db, voucher: failingVoucherService);

        var refundResult = await service.RefundOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.ServerError, refundResult.Outcome);

        // Status pesanan wajib ter-rollback kembali ke NEEDS_ACTION.
        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, order.Status);
        Assert.True(OrderFulfillmentState.HasActiveVoucherReservation(order));
    }

    // ── PHASE E SCENARIOS 4, 5, 6, 7: DigiflazzProvider Response Classification ─

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{\"message\":\"Internal Server Error\"}")]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"message\":\"Rate limited\"}")]
    [InlineData(HttpStatusCode.BadGateway, "<html>502 Bad Gateway</html>")]
    [InlineData(HttpStatusCode.OK, "")]
    [InlineData(HttpStatusCode.OK, "{invalid-json")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"data\":null}")]
    [InlineData(HttpStatusCode.OK, "{\"data\":{}}")]
    [InlineData(HttpStatusCode.OK, "{\"data\":{\"message\":\"Tanpa field status\"}}")]
    [InlineData(HttpStatusCode.OK, "{\"data\":{\"status\":\"UnknownStatus\"}}")]
    [InlineData(HttpStatusCode.OK, "{\"data\":{\"status\":\"Sukses\",\"sn\":\"\"}}")]
    public async Task DigiflazzProvider_WhenResponseIsAmbiguousOrNon2xxOrMissingFields_ThrowsUncertain_AndNeverMarksProcessingOrCompleted(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });
        var provider = CreateRealDigiflazzProvider(handler);

        await Assert.ThrowsAsync<DigiflazzUncertainOutcomeException>(() =>
            provider.TopupAsync("sku-1", "123456789", OrderNo));

        await Assert.ThrowsAsync<DigiflazzUncertainOutcomeException>(() =>
            provider.CheckStatusAsync("sku-1", "123456789", OrderNo));

        // Verifikasi juga integrasi penuh dengan FulfillmentService: pesanan PAID harus masuk NEEDS_ACTION (UncertainOutcome),
        // TIDAK pernah masuk PROCESSING atau COMPLETED.
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-1", OrderNo, "qris", "settlement", "accept", "200", "15199.00", null)
        };
        var service = CreateService(db, midtrans: midtrans, digiflazz: provider);

        await service.HandleNotificationAsync(CreateSignedNotification(OrderNo, "settlement"));

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, order.Status);
        Assert.Equal(SupplierFulfillmentState.UncertainOutcome, OrderFulfillmentState.GetSupplierState(order));
    }

    [Fact]
    public async Task Fulfillment_WhenCustomProviderReturnsNullFromTopupAsync_TreatsAsUncertain_NeverProcessing()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-null-sn", OrderNo, "qris", "settlement", "accept", "200", "15199.00", null)
        };
        var digiflazz = new StubDigiflazzProvider { SnResult = null };
        var service = CreateService(db, midtrans: midtrans, digiflazz: digiflazz);

        await service.HandleNotificationAsync(
            CreateSignedNotification(OrderNo, "settlement", transactionId: "tx-null-sn"));

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, order.Status);
        Assert.Equal(SupplierFulfillmentState.UncertainOutcome, OrderFulfillmentState.GetSupplierState(order));
    }

    [Theory]
    [InlineData("{\"data\":{\"status\":\"Gagal\",\"message\":\"Produk sedang gangguan\"}}")]
    [InlineData("{\"data\":{\"status\":\"Gagal\"}}")]
    [InlineData("{\"data\":{\"status\":\"Gagal\",\"message\":\"\"}}")]
    public async Task DigiflazzProvider_WhenStatusIsGagalWithOrWithoutMessage_TreatsAsTerminalFailure(
        string responseBody)
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });
        var provider = CreateRealDigiflazzProvider(handler);

        await Assert.ThrowsAsync<DigiflazzTerminalFailureException>(() =>
            provider.TopupAsync("sku-1", "123456789", OrderNo));

        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-gagal", OrderNo, "qris", "settlement", "accept", "200", "15199.00", null)
        };
        var service = CreateService(db, midtrans: midtrans, digiflazz: provider);

        await service.HandleNotificationAsync(
            CreateSignedNotification(OrderNo, "settlement", transactionId: "tx-gagal"));

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, order.Status);
        Assert.Equal(SupplierFulfillmentState.ConfirmedFailure, OrderFulfillmentState.GetSupplierState(order));
    }

    [Fact]
    public async Task DigiflazzProvider_WhenStatusIsPending_TreatsAsProcessing()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"data\":{\"status\":\"Pending\",\"message\":\"Transaksi sedang diproses\"}}",
                Encoding.UTF8,
                "application/json")
        });
        var provider = CreateRealDigiflazzProvider(handler);

        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-pend", OrderNo, "qris", "settlement", "accept", "200", "15199.00", null)
        };
        var service = CreateService(db, midtrans: midtrans, digiflazz: provider);

        await service.HandleNotificationAsync(
            CreateSignedNotification(OrderNo, "settlement", transactionId: "tx-pend"));

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Processing, order.Status);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DigiflazzProvider_WhenStatusIsSuksesWithValidSn_TreatsAsCompleted()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"data\":{\"status\":\"Sukses\",\"sn\":\"SN-VALID-777\",\"message\":\"Transaksi berhasil\"}}",
                Encoding.UTF8,
                "application/json")
        });
        var provider = CreateRealDigiflazzProvider(handler);

        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(AppConstants.OrderStatus.Pending));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-sukses", OrderNo, "qris", "settlement", "accept", "200", "15199.00", null)
        };
        var service = CreateService(db, midtrans: midtrans, digiflazz: provider);

        await service.HandleNotificationAsync(
            CreateSignedNotification(OrderNo, "settlement", transactionId: "tx-sukses"));

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Completed, order.Status);
        Assert.Equal("SN-VALID-777", order.Sn);
        Assert.Equal(1, handler.CallCount);
    }

    // ── PHASE E SCENARIO 8: Never-Submitted Late-Paid Order Single Retry ─

    [Fact]
    public async Task RetryOrderAsync_WhenLatePaidNeverSubmittedOrderReturnsPending_SendsSingleSubmission_TransitionsToProcessing_AndBlocksRefund()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: OrderFulfillmentState.CreateNeverSubmittedTimestamp(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            SnResult = string.Empty // AcceptedPending ("Pending")
        };
        var service = CreateService(db, digiflazz: digiflazz);

        var retryResult = await service.RetryOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.Success, retryResult.Outcome);
        Assert.Equal(1, digiflazz.TopupCalls);
        Assert.Equal(0, digiflazz.CheckStatusCalls); // TIDAK boleh memanggil CheckStatusAsync sebelum TopupAsync!

        var orderAfterRetry = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Processing, orderAfterRetry.Status);

        // Selama pesanan berstatus PROCESSING, Refund wajib ditolak.
        var refundResult = await service.RefundOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.InvalidState, refundResult.Outcome);
    }

    [Fact]
    public async Task RetryOrderAsync_WhenLatePaidNeverSubmittedOrderReturnsSukses_SendsSingleSubmission_AndMarksCompleted()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: OrderFulfillmentState.CreateNeverSubmittedTimestamp(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            SnResult = "SN-LATE-SINGLE-1"
        };
        var service = CreateService(db, digiflazz: digiflazz);

        var retryResult = await service.RetryOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.Success, retryResult.Outcome);
        Assert.Equal(1, digiflazz.TopupCalls);
        Assert.Equal(0, digiflazz.CheckStatusCalls);

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.Completed, order.Status);
        Assert.Equal("SN-LATE-SINGLE-1", order.Sn);
    }

    [Fact]
    public async Task RetryOrderAsync_WhenLatePaidNeverSubmittedOrderThrowsTimeout_MarksUncertain_AndBlocksRefund()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.NeedsAction,
            processedAt: OrderFulfillmentState.CreateNeverSubmittedTimestamp(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var digiflazz = new StubDigiflazzProvider
        {
            TopupException = new DigiflazzUncertainOutcomeException("Timeout")
        };
        var service = CreateService(db, digiflazz: digiflazz);

        var retryResult = await service.RetryOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.Error, retryResult.Outcome);
        Assert.Equal(1, digiflazz.TopupCalls);
        Assert.Equal(0, digiflazz.CheckStatusCalls);

        var order = await ReloadAsync(db);
        Assert.Equal(AppConstants.OrderStatus.NeedsAction, order.Status);
        Assert.Equal(SupplierFulfillmentState.UncertainOutcome, OrderFulfillmentState.GetSupplierState(order));

        // Karena hasil pengajuan masih ambigu (UncertainOutcome), Refund wajib ditolak sampai direkonsiliasi.
        var refundResult = await service.RefundOrderAsync(OrderNo);
        Assert.Equal(OrderActionOutcome.InvalidState, refundResult.Outcome);
    }

    // ── PHASE E SCENARIO 10: Concurrency Invariants ──────────────────

    [Fact]
    public async Task Concurrency_TwoConcurrentRetriesOnNeverSubmittedOrder_SendsAtMostOneTopupRequest()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"digitalin-concurrency-retry-{Guid.NewGuid():N}.db");
        try
        {
            var connString = $"Data Source={dbPath};Pooling=False";
            DigitalinDbContext CreateContext() =>
                new(new DbContextOptionsBuilder<DigitalinDbContext>().UseSqlite(connString).Options);

            await using (var setupDb = CreateContext())
            {
                await setupDb.Database.EnsureCreatedAsync();
                setupDb.GameProducts.Add(new GameProduct
                {
                    Name = "5280 CP",
                    GameCode = "TESTGAME",
                    BuyerSkuCode = "sku-1",
                    Brand = "TESTGAME",
                    Category = "Games",
                    Price = Amount,
                    IsActive = true
                });
                setupDb.TopupOrders.Add(NewStuckOrder(
                    AppConstants.OrderStatus.NeedsAction,
                    processedAt: OrderFulfillmentState.CreateNeverSubmittedTimestamp(DateTime.UtcNow)));
                await setupDb.SaveChangesAsync();
            }

            var sharedDigiflazz = new StubDigiflazzProvider { SnResult = "SN-CONCURRENT-OK" };

            await using var db1 = CreateContext();
            await using var db2 = CreateContext();
            var svc1 = CreateService(db1, digiflazz: sharedDigiflazz);
            var svc2 = CreateService(db2, digiflazz: sharedDigiflazz);

            var r1 = await svc1.RetryOrderAsync(OrderNo);
            var r2 = await svc2.RetryOrderAsync(OrderNo);

            Assert.Equal(1, sharedDigiflazz.TopupCalls);
            Assert.Equal(0, sharedDigiflazz.CheckStatusCalls);
            Assert.Contains(OrderActionOutcome.Success, new[] { r1.Outcome, r2.Outcome });
            Assert.Contains(OrderActionOutcome.InvalidState, new[] { r1.Outcome, r2.Outcome });
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Concurrency_ExpireWebhookAndSweepOnPendingOrder_ReleasesVoucherAtMostOnce()
    {
        await using var testDb = await SweepTestDb.CreateAsync();
        var db = testDb.Db;
        var realVoucherService = new VoucherService(db);

        db.Vouchers.Add(new VoucherEntity
        {
            Code = "CONCVOUCH",
            DiscountType = "FixedAmount",
            DiscountValue = 1000m,
            MinimumTransaction = 5000m,
            UsageLimit = 5,
            UsedCount = 2, // 1 milik order ini, 1 milik order lain
            IsActive = true,
            ExpiredAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        db.TopupOrders.Add(NewStuckOrder(
            AppConstants.OrderStatus.Pending,
            voucherCode: "CONCVOUCH",
            orderDate: DateTime.UtcNow.AddMinutes(-50)));
        await db.SaveChangesAsync();

        var midtrans = new StubMidtransProvider
        {
            StatusResult = new MidtransTransactionStatus(
                "tx-exp-1", OrderNo, "qris", "expire", "accept", "407", "15199.00", null)
        };
        var service = CreateService(db, midtrans: midtrans, voucher: realVoucherService);

        await service.HandleNotificationAsync(CreateSignedNotification(OrderNo, "expire", statusCode: "407"));
        await service.SweepStuckOrdersAsync();

        var voucherAfter = await db.Vouchers.AsNoTracking().SingleAsync(v => v.Code == "CONCVOUCH");
        Assert.Equal(1, voucherAfter.UsedCount); // hanya berkurang 1x (dari 2 menjadi 1)
    }
}
