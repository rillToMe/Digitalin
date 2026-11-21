using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Unit test InvoiceLookupService (Cek Transaksi publik) dengan EF InMemory.
/// Menutup: lookup by OrderNo, mapping status pembayaran untuk seluruh status
/// order, expiresAt hanya saat PENDING, flag kepemilikan (optional-auth),
/// guest order, dan bentuk respons yang hanya berisi field aman.
/// </summary>
public class InvoiceLookupServiceTests
{
    private const int DefaultExpiryMinutes = AppConstants.SystemSettings.MidtransExpiryDefaultMinutes;

    /// <summary>Stub IAppSettingsService — hanya expiry yang dipakai service.</summary>
    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public int ExpiryMinutes { get; set; } = DefaultExpiryMinutes;

        public Task<string?> GetAsync(string key, string? defaultValue = null) =>
            Task.FromResult(defaultValue);

        public Task SetAsync(string key, string value) => Task.CompletedTask;

        public Task<int> GetMidtransExpiryMinutesAsync() => Task.FromResult(ExpiryMinutes);

        public Task<bool> IsMaintenanceEnabledAsync() => Task.FromResult(false);

        public Task<MaintenanceConfig> GetMaintenanceConfigAsync() =>
            Task.FromResult(new MaintenanceConfig(false, "", "", null, true));

        public Task UpdateMaintenanceConfigAsync(MaintenanceConfig config) => Task.CompletedTask;
    }

    private static DigitalinDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DigitalinDbContext(options);
    }

    /// <summary>Kunci HMAC test — bukan rahasia produksi, hanya untuk derivasi deterministik di unit test.</summary>
    private const string TestHashSecret = "unit-test-hmac-secret-0123456789abcdef";

    private static TopupOrder NewOrder(
        string orderNo = "INV-20260906-ABCDEF1234",
        string status = AppConstants.OrderStatus.Pending,
        int? customerId = null,
        decimal totalPaid = 50000,
        DateTime? orderDate = null,
        string? publicHash = null) =>
        new()
        {
            OrderNo = orderNo,
            PublicHash = publicHash ?? OrderPublicHash.Compute(orderNo, TestHashSecret),
            PlayerId = "player-secret",
            Status = status,
            OrderDate = orderDate ?? new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
            CustomerId = customerId,
            GameProductId = 1,
            ProductName = "5280 CP",
            BuyerSkuCode = "sku",
            BrandName = "CALL OF DUTY MOBILE",
            BrandSlug = "call-of-duty-mobile",
            CategoryName = "Game",
            ImageUrl = "https://x.test/cover.png",
            Sn = "SN-RAHASIA",
            DiscountAmount = 1000,
            OriginalPrice = 51000,
            TotalPaid = totalPaid,
            CostPrice = 40000,
            ProfitAmount = 10000
        };

    private static InvoiceLookupService CreateService(DigitalinDbContext db, int expiryMinutes = DefaultExpiryMinutes) =>
        new(db, new StubAppSettingsService { ExpiryMinutes = expiryMinutes });

    // ── Lookup dasar (PublicHash = identifier publik satu-satunya) ──

    [Fact]
    public async Task GetPublicInvoiceAsync_Found_ReturnsSafeViewModel()
    {
        await using var db = CreateDb();
        var order = NewOrder();
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!);

        Assert.NotNull(result);
        Assert.Equal("INV-20260906-ABCDEF1234", result!.InvoiceNumber);
        Assert.Equal("5280 CP", result.ProductName);
        Assert.Equal("https://x.test/cover.png", result.ImageUrl);
        Assert.Equal(50000m, result.Amount);
        Assert.Equal(AppConstants.OrderStatus.Pending, result.Status);
        Assert.Equal(PaymentStatus.Unpaid, result.PaymentStatus);
        Assert.Equal(new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc), result.CreatedAt);
        Assert.False(result.BelongsToAccount);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_NotFound_ReturnsNull()
    {
        await using var db = CreateDb();

        // Hash valid-format milik order lain → null → 404 seragam.
        var otherHash = OrderPublicHash.Compute("INV-20260906-ZZZZZZ9999", TestHashSecret);

        var result = await CreateService(db).GetPublicInvoiceAsync(otherHash);

        Assert.Null(result);
    }

    /// <summary>
    /// Kunci kontrak baru: OrderNo (URL status lama /order/status/{OrderNo})
    /// TIDAK diterima endpoint status publik — harus resolve dulu.
    /// </summary>
    [Fact]
    public async Task GetPublicInvoiceAsync_OrderNo_Rejected_ReturnsNull()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_TrimsInput()
    {
        await using var db = CreateDb();
        var order = NewOrder();
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync($"  {order.PublicHash}  ");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_UppercaseHash_NormalizedToLookup()
    {
        await using var db = CreateDb();
        var order = NewOrder();
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!.ToUpperInvariant());

        Assert.NotNull(result);
        Assert.Equal(order.OrderNo, result!.InvoiceNumber);
    }

    [Theory]
    [InlineData("bukan-format-hash")]
    [InlineData("INV-20260906-ABCDEF1234")] // OrderNo (URL lama) → ditolak
    [InlineData("ABC")] // hash pendek
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")] // 64 char non-hex
    [InlineData("")] // kosong
    public async Task GetPublicInvoiceAsync_InvalidReference_ReturnsNull(string reference)
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(reference);

        Assert.Null(result);
    }

    // ── Resolve (Cek Transaksi: OrderNo → PublicHash) ──

    [Fact]
    public async Task ResolvePublicHashAsync_Found_ReturnsHashOnly()
    {
        await using var db = CreateDb();
        var order = NewOrder();
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var hash = await CreateService(db).ResolvePublicHashAsync("INV-20260906-ABCDEF1234");

        Assert.Equal(order.PublicHash, hash);
    }

    [Fact]
    public async Task ResolvePublicHashAsync_Guest_AnonymousLookup()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder(customerId: null));
        await db.SaveChangesAsync();

        // Tanpa auth apa pun — Cek Transaksi tetap publik.
        var hash = await CreateService(db).ResolvePublicHashAsync("INV-20260906-ABCDEF1234");

        Assert.NotNull(hash);
    }

    [Fact]
    public async Task ResolvePublicHashAsync_TrimsInput()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var hash = await CreateService(db).ResolvePublicHashAsync("  INV-20260906-ABCDEF1234  ");

        Assert.NotNull(hash);
    }

    [Fact]
    public async Task ResolvePublicHashAsync_NotFound_ReturnsNull()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var hash = await CreateService(db).ResolvePublicHashAsync("INV-20260906-TIDAKADA");

        Assert.Null(hash);
    }

    [Fact]
    public async Task ResolvePublicHashAsync_RowWithNullPublicHash_ReturnsNull()
    {
        await using var db = CreateDb();
        // Baris pra-backfill (PublicHash NULL) — tidak bisa diresolve sampai
        // startup backfill mengisinya; diperlakukan tidak-ditemukan.
        var order = NewOrder();
        order.PublicHash = null;
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var hash = await CreateService(db).ResolvePublicHashAsync("INV-20260906-ABCDEF1234");

        Assert.Null(hash);
    }

    // ── Mapping status pembayaran ──

    [Theory]
    [InlineData(AppConstants.OrderStatus.Pending, PaymentStatus.Unpaid)]
    [InlineData(AppConstants.OrderStatus.Paid, PaymentStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Processing, PaymentStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Completed, PaymentStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.NeedsAction, PaymentStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Expired, PaymentStatus.Failed)]
    [InlineData(AppConstants.OrderStatus.Failed, PaymentStatus.Failed)]
    [InlineData(AppConstants.OrderStatus.Refunded, PaymentStatus.Refunded)]
    public async Task GetPublicInvoiceAsync_PaymentStatusMapping(string orderStatus, string expectedPaymentStatus)
    {
        await using var db = CreateDb();
        var order = NewOrder(status: orderStatus);
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!);

        Assert.Equal(expectedPaymentStatus, result!.PaymentStatus);
        Assert.Equal(orderStatus, result.Status);
    }

    // ── Expiry (hanya PENDING) ──

    [Fact]
    public async Task GetPublicInvoiceAsync_Pending_ExpiresAtFromAppSettings()
    {
        await using var db = CreateDb();
        var order = NewOrder(status: AppConstants.OrderStatus.Pending);
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db, expiryMinutes: 45).GetPublicInvoiceAsync(order.PublicHash!);

        Assert.NotNull(result);
        Assert.Equal(order.OrderDate.AddMinutes(45), result!.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, result.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAt!.Value.Kind);
    }

    [Theory]
    [InlineData(AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.Expired)]
    public async Task GetPublicInvoiceAsync_NotPending_NoExpiry(string status)
    {
        await using var db = CreateDb();
        var order = NewOrder(status: status);
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!);

        Assert.Null(result!.ExpiresAt);
    }

    // ── Instruksi pembayaran Core API (PENDING saja) ──

    private const string QrisInstructionsJson =
        """{"qrString":"00020101QRIS","actions":[{"name":"generate-qr-code","url":"https://api.midtrans.com/v2/qris/x/qr-code"}],"vaBank":null,"vaNumber":null}""";

    [Fact]
    public async Task GetPublicInvoiceAsync_Pending_ExposesPaymentTypeAndInstructions()
    {
        await using var db = CreateDb();
        var order = NewOrder(status: AppConstants.OrderStatus.Pending);
        order.PaymentType = "qris";
        order.PaymentInstructionsJson = QrisInstructionsJson;
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!);

        Assert.Equal("qris", result!.PaymentType);
        Assert.NotNull(result.PaymentInstructions);
        Assert.Equal("00020101QRIS", result.PaymentInstructions!.QrString);
        var action = Assert.Single(result.PaymentInstructions.Actions);
        Assert.Equal("generate-qr-code", action.Name);
        Assert.Null(result.PaymentInstructions.VaBank);
        Assert.Null(result.PaymentInstructions.VaNumber);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_NotPendingCoreApiOrder_NoPaymentTypeOrInstructions()
    {
        await using var db = CreateDb();
        var order = NewOrder(status: AppConstants.OrderStatus.Paid);
        order.PaymentType = "qris";
        order.PaymentInstructionsJson = QrisInstructionsJson;
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!);

        Assert.Null(result!.PaymentType);
        Assert.Null(result.PaymentInstructions);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_PendingWithoutCoreFields_NoPaymentTypeOrInstructions()
    {
        await using var db = CreateDb();
        // Order pra-migrasi tanpa kolom Core API — tidak ada yang bocor.
        var order = NewOrder(status: AppConstants.OrderStatus.Pending);
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!);

        Assert.Null(result!.PaymentType);
        Assert.Null(result.PaymentInstructions);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_PendingCoreApiOrder_SerializedResponseStaysSafe()
    {
        await using var db = CreateDb();
        var order = NewOrder(status: AppConstants.OrderStatus.Pending);
        order.PaymentType = "bank_transfer";
        order.PaymentInstructionsJson =
            """{"qrString":null,"actions":[],"vaBank":"bca","vaNumber":"91019021581"}""";
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!);

        // Instruksi VA memang sengaja terekspos (aksi bayar PENDING), tapi
        // rahasia order lain tetap tidak boleh bocor.
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Equal("bca", result!.PaymentInstructions!.VaBank);
        Assert.Equal("91019021581", result.PaymentInstructions.VaNumber);
        Assert.DoesNotContain("player-secret", serialized);
        Assert.DoesNotContain("SN-RAHASIA", serialized);
        Assert.DoesNotContain("CustomerId", serialized);
    }

    // ── Kepemilikan (optional-auth) ──

    [Fact]
    public async Task GetPublicInvoiceAsync_OwnerSeesBelongsToAccount()
    {
        await using var db = CreateDb();
        var order = NewOrder(customerId: 7);
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var own = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!, currentCustomerId: 7);
        var other = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!, currentCustomerId: 99);

        Assert.True(own!.BelongsToAccount);
        Assert.False(other!.BelongsToAccount);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_GuestOrder_AnonymousLookup()
    {
        await using var db = CreateDb();
        var order = NewOrder(customerId: null);
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        // Guest (tanpa JWT) tetap bisa cek invoice milik guest/other.
        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!);

        Assert.NotNull(result);
        Assert.False(result!.BelongsToAccount);
    }

    // ── Privasi: bentuk DTO hanya field aman ──

    [Fact]
    public async Task GetPublicInvoiceAsync_CompletedOrder_ResponseContainsNoSensitiveFields()
    {
        await using var db = CreateDb();
        // Status terminal: tanpa instruksi bayar — tidak ada rahasia order yang boleh bocor.
        db.TopupOrders.Add(NewOrder(status: AppConstants.OrderStatus.Completed, customerId: 7));
        await db.SaveChangesAsync();

        var completed = await db.TopupOrders.AsNoTracking().SingleAsync();
        var result = await CreateService(db).GetPublicInvoiceAsync(completed.PublicHash!, currentCustomerId: 7);

        // DTO hanya boleh punya field publik-aman.
        var props = typeof(PublicInvoiceResponse).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();
        Assert.Equal(
            new[] { "Amount", "BelongsToAccount", "BrandSlug", "CreatedAt", "ExpiresAt", "ImageUrl", "InvoiceNumber", "PaymentInstructions", "PaymentStatus", "PaymentType", "ProductName", "Status" },
            props);
        // BrandSlug = identifier katalog publik (URL /game/{slug}) — boleh
        // terekspos; bukan data transaksi privat.
        Assert.Equal("call-of-duty-mobile", result!.BrandSlug);

        // Serialize penuh — rahasia order tidak boleh bocor.
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("player-secret", serialized);
        Assert.DoesNotContain("SN-RAHASIA", serialized);
        Assert.DoesNotContain("CustomerId", serialized);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_Pending_StillHidesNonPaymentSecrets()
    {
        await using var db = CreateDb();
        var order = NewOrder(status: AppConstants.OrderStatus.Pending, customerId: 7);
        order.PaymentType = "qris";
        order.PaymentInstructionsJson = QrisInstructionsJson;
        db.TopupOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync(order.PublicHash!, currentCustomerId: 7);

        // Instruksi bayar memang sengaja terekspos (aksi PENDING) — rahasia
        // order lain tetap tidak boleh ikut.
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.NotNull(result!.PaymentInstructions);
        Assert.DoesNotContain("player-secret", serialized);
        Assert.DoesNotContain("SN-RAHASIA", serialized);
        Assert.DoesNotContain("CustomerId", serialized);
    }
}
