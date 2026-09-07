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

    private static TopupOrder NewOrder(
        string orderNo = "INV-20260906-ABCDEF1234",
        string status = AppConstants.OrderStatus.Pending,
        int? customerId = null,
        decimal totalPaid = 50000,
        DateTime? orderDate = null) =>
        new()
        {
            OrderNo = orderNo,
            PlayerId = "player-secret",
            Status = status,
            OrderDate = orderDate ?? new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
            CustomerId = customerId,
            GameProductId = 1,
            SnapToken = "snap-secret-token",
            RedirectUrl = "https://midtrans/secret",
            ProductName = "5280 CP",
            BuyerSkuCode = "sku",
            BrandName = "CALL OF DUTY MOBILE",
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

    // ── Lookup dasar ──

    [Fact]
    public async Task GetPublicInvoiceAsync_Found_ReturnsSafeViewModel()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234");

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

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-TIDAK-ADA");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_TrimsInput()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder());
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("  INV-20260906-ABCDEF1234  ");

        Assert.NotNull(result);
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
        db.TopupOrders.Add(NewOrder(status: orderStatus));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234");

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

        var result = await CreateService(db, expiryMinutes: 45).GetPublicInvoiceAsync(order.OrderNo);

        Assert.NotNull(result);
        Assert.Equal(order.OrderDate.AddMinutes(45), result!.ExpiresAt);
    }

    [Theory]
    [InlineData(AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.Expired)]
    public async Task GetPublicInvoiceAsync_NotPending_NoExpiry(string status)
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder(status: status));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234");

        Assert.Null(result!.ExpiresAt);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_Pending_ExposesSnapTokenForPayment()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder(status: AppConstants.OrderStatus.Pending));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234");

        // Token pembayaran (bukan kredensial) hanya untuk PENDING — agar
        // halaman status bisa melanjutkan pembayaran (level kepercayaan
        // sama dengan POST /orders yang guest juga menerima snap token).
        Assert.Equal("snap-secret-token", result!.SnapToken);
    }

    [Theory]
    [InlineData(AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.Expired)]
    public async Task GetPublicInvoiceAsync_NotPending_NoSnapToken(string status)
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder(status: status));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234");

        Assert.Null(result!.SnapToken);
    }

    // ── Kepemilikan (optional-auth) ──

    [Fact]
    public async Task GetPublicInvoiceAsync_OwnerSeesBelongsToAccount()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder(customerId: 7));
        await db.SaveChangesAsync();

        var own = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234", currentCustomerId: 7);
        var other = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234", currentCustomerId: 99);

        Assert.True(own!.BelongsToAccount);
        Assert.False(other!.BelongsToAccount);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_GuestOrder_AnonymousLookup()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder(customerId: null));
        await db.SaveChangesAsync();

        // Guest (tanpa JWT) tetap bisa cek invoice milik guest/other.
        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234");

        Assert.NotNull(result);
        Assert.False(result!.BelongsToAccount);
    }

    // ── Privasi: bentuk DTO hanya field aman ──

    [Fact]
    public async Task GetPublicInvoiceAsync_CompletedOrder_ResponseContainsNoSensitiveFields()
    {
        await using var db = CreateDb();
        // Status terminal: tanpa snapToken — tidak ada rahasia order yang boleh bocor.
        db.TopupOrders.Add(NewOrder(status: AppConstants.OrderStatus.Completed, customerId: 7));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234", currentCustomerId: 7);

        // DTO hanya boleh punya field publik-aman.
        var props = typeof(PublicInvoiceResponse).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();
        Assert.Equal(
            new[] { "Amount", "BelongsToAccount", "CreatedAt", "ExpiresAt", "ImageUrl", "InvoiceNumber", "PaymentStatus", "ProductName", "SnapToken", "Status" },
            props);

        // Serialize penuh — rahasia order tidak boleh bocor.
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("player-secret", serialized);
        Assert.DoesNotContain("snap-secret-token", serialized);
        Assert.DoesNotContain("SN-RAHASIA", serialized);
        Assert.DoesNotContain("CustomerId", serialized);
        Assert.DoesNotContain("RedirectUrl", serialized);
    }

    [Fact]
    public async Task GetPublicInvoiceAsync_Pending_StillHidesNonPaymentSecrets()
    {
        await using var db = CreateDb();
        db.TopupOrders.Add(NewOrder(status: AppConstants.OrderStatus.Pending, customerId: 7));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetPublicInvoiceAsync("INV-20260906-ABCDEF1234", currentCustomerId: 7);

        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Equal("snap-secret-token", result!.SnapToken); // token bayar memang sengaja
        Assert.DoesNotContain("player-secret", serialized);
        Assert.DoesNotContain("SN-RAHASIA", serialized);
        Assert.DoesNotContain("CustomerId", serialized);
        Assert.DoesNotContain("RedirectUrl", serialized);
    }
}
