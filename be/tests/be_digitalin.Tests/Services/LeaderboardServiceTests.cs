using System.Reflection;
using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Unit test LeaderboardService dengan EF InMemory provider. Menutup:
/// agregasi per customer, sort turun + tie-break, top-10, eligibility status
/// (lunak masuk; PENDING/EXPIRED/FAILED/REFUNDED keluar), guest dikecualikan,
/// batas periode (tepat di awal masuk, sesaat sebelum keluar), privasi
/// (hanya 4 field, nama bentuk email disamarkan), dan parser periode.
/// </summary>
public class LeaderboardServiceTests
{
    private static readonly TimeZoneInfo Wib =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    private static DigitalinDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DigitalinDbContext(options);
    }

    private static Customer NewCustomer(
        string fullName,
        string? avatarUrl = null,
        string email = "user@x.test") =>
        new() { Email = email, FullName = fullName, PasswordHash = "h", AvatarUrl = avatarUrl };

    /// <summary>Order luna dengan tanggal bebas — field lain diisi minimal.</summary>
    private static TopupOrder PaidOrder(
        int customerId,
        decimal totalPaid,
        DateTime orderDate,
        string status = AppConstants.OrderStatus.Completed) =>
        new()
        {
            OrderNo = Guid.NewGuid().ToString("N"),
            PlayerId = "p",
            Status = status,
            OrderDate = orderDate,
            CustomerId = customerId,
            ProductName = "prod",
            BuyerSkuCode = "sku",
            BrandName = "brand",
            CategoryName = "cat",
            OriginalPrice = totalPaid,
            TotalPaid = totalPaid,
            CostPrice = 0,
            ProfitAmount = 0
        };

    private static (DateTime StartUtc, DateTime NowUtc) TodayWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Wib).Date, Wib);
        return (startUtc, nowUtc);
    }

    private static LeaderboardService CreateService(DigitalinDbContext db) => new(db);

    // ── Agregasi, sort, tie-break, limit ──

    [Fact]
    public async Task GetLeaderboardAsync_SumsMultipleOrdersPerCustomer_Descending()
    {
        await using var db = CreateDb();
        var (start, now) = TodayWindow();
        var budi = NewCustomer("Budi"); var sinta = NewCustomer("Sinta");
        db.Customers.AddRange(budi, sinta);
        // Budi: 2 order (5000 + 7000 = 12000), Sinta: 1 order (10000).
        db.TopupOrders.AddRange(
            PaidOrder(budi.Id, 5000, start.AddHours(1)),
            PaidOrder(budi.Id, 7000, start.AddHours(2)),
            PaidOrder(sinta.Id, 10000, start.AddHours(3)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        Assert.Equal(2, result.Count);
        Assert.Equal("Budi", result[0].DisplayName);       // 12000 > 10000
        Assert.Equal(12000m, result[0].TotalSpent);
        Assert.Equal(1, result[0].Rank);
        Assert.Equal("Sinta", result[1].DisplayName);
        Assert.Equal(10000m, result[1].TotalSpent);
        Assert.Equal(2, result[1].Rank);
    }

    [Fact]
    public async Task GetLeaderboardAsync_EqualTotal_TieBreakByCustomerId()
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        // Id lebih kecil dibuat belakangan supaya bukan urutan insert yang
        // kebetulan menang — tie-break HARUS dari CustomerId.
        var late = NewCustomer("Zed"); db.Customers.Add(late); await db.SaveChangesAsync();
        var early = NewCustomer("Alpha"); db.Customers.Add(early); await db.SaveChangesAsync();

        db.TopupOrders.AddRange(
            PaidOrder(late.Id, 5000, start.AddMinutes(1)),
            PaidOrder(early.Id, 5000, start.AddMinutes(2)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        // early.Id < late.Id → "Alpha" (Zed) di atas walaupun namanya lebih akhir alfabetis.
        Assert.Equal(early.Id < late.Id ? "Alpha" : "Zed", result[0].DisplayName);
        Assert.Equal(2, result.Count);
        Assert.Equal(result[0].TotalSpent, result[1].TotalSpent);
    }

    [Fact]
    public async Task GetLeaderboardAsync_MoreThanTenCustomers_OnlyTopTen()
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        // 11 customer, spending 1000..11000 — yang keluar hanya 10 terbesar.
        for (var i = 1; i <= 11; i++)
        {
            var c = NewCustomer($"Cust{i}");
            db.Customers.Add(c);
            await db.SaveChangesAsync();
            db.TopupOrders.Add(PaidOrder(c.Id, i * 1000, start.AddMinutes(i)));
        }
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        Assert.Equal(10, result.Count);
        Assert.Equal(11000m, result[0].TotalSpent);   // Cust11
        Assert.Equal(2000m, result[9].TotalSpent);    // Cust2; Cust1 (1000) terdorong keluar
        Assert.Equal(Enumerable.Range(1, 10), result.Select(e => e.Rank));
    }

    [Fact]
    public async Task GetLeaderboardAsync_ZeroEligible_EmptyResult()
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var c = NewCustomer("Budi");
        var pending = PaidOrder(c.Id, 5000, start.AddMinutes(1), AppConstants.OrderStatus.Pending);
        db.Customers.Add(c);
        db.TopupOrders.Add(pending);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        Assert.Empty(result);
    }

    // ── Eligibility status ──

    [Theory]
    [InlineData(AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Processing)]
    [InlineData(AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.NeedsAction)]
    public async Task GetLeaderboardAsync_PostPaymentStatusesIncluded(string status)
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var c = NewCustomer("Budi");
        db.Customers.Add(c);
        db.TopupOrders.Add(PaidOrder(c.Id, 5000, start.AddMinutes(1), status));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        var entry = Assert.Single(result);
        Assert.Equal(5000m, entry.TotalSpent);
    }

    [Theory]
    [InlineData(AppConstants.OrderStatus.Pending)]
    [InlineData(AppConstants.OrderStatus.Expired)]
    [InlineData(AppConstants.OrderStatus.Failed)]
    [InlineData(AppConstants.OrderStatus.Refunded)]
    public async Task GetLeaderboardAsync_NonPaidStatusesExcluded(string status)
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var c = NewCustomer("Budi");
        db.Customers.Add(c);
        db.TopupOrders.Add(PaidOrder(c.Id, 5000, start.AddMinutes(1), status));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLeaderboardAsync_GuestOrdersExcluded()
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var guest = PaidOrder(999, 99999, start.AddMinutes(1));
        guest.CustomerId = null; // order tamu — tidak boleh jadi identitas palsu
        db.TopupOrders.Add(guest);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLeaderboardAsync_MixedStatuses_OnlyPaidSummed()
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var c = NewCustomer("Budi");
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        db.TopupOrders.AddRange(
            PaidOrder(c.Id, 10000, start.AddMinutes(1), AppConstants.OrderStatus.Completed),
            PaidOrder(c.Id, 40000, start.AddMinutes(2), AppConstants.OrderStatus.Refunded),
            PaidOrder(c.Id, 20000, start.AddMinutes(3), AppConstants.OrderStatus.Pending));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        var entry = Assert.Single(result);
        Assert.Equal(10000m, entry.TotalSpent); // hanya order lunak
    }

    // ── Batas periode (setengah-terbuka, kalender WIB) ──

    [Fact]
    public async Task GetLeaderboardAsync_TodayBoundary()
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var c = NewCustomer("Budi");
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        // Sesaat sebelum 00:00 WIB = kemarin → keluar; tepat 00:00 WIB = hari ini → masuk.
        db.TopupOrders.AddRange(
            PaidOrder(c.Id, 1, start.AddSeconds(-1)),
            PaidOrder(c.Id, 2, start));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        var entry = Assert.Single(result);
        Assert.Equal(2m, entry.TotalSpent);
    }

    [Fact]
    public async Task GetLeaderboardAsync_WeekBoundary_MondayStart()
    {
        await using var db = CreateDb();
        var nowUtc = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Wib);
        var daysFromMonday = ((int)localNow.DayOfWeek + 6) % 7;
        var mondayLocal = localNow.Date.AddDays(-daysFromMonday);
        var mondayUtc = TimeZoneInfo.ConvertTimeToUtc(mondayLocal, Wib);

        var c = NewCustomer("Budi");
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        // Minggu 23:59:59 WIB (sebelum Senin) vs Senin 00:00 WIB.
        db.TopupOrders.AddRange(
            PaidOrder(c.Id, 1, mondayUtc.AddSeconds(-1)),
            PaidOrder(c.Id, 2, mondayUtc));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Week);

        var entry = Assert.Single(result);
        Assert.Equal(2m, entry.TotalSpent);
    }

    [Fact]
    public async Task GetLeaderboardAsync_MonthBoundary_FirstDayStart()
    {
        await using var db = CreateDb();
        var nowUtc = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Wib);
        var firstOfThisMonthLocal = new DateTime(localNow.Year, localNow.Month, 1);
        var lastOfPrevMonthLocal = firstOfThisMonthLocal.AddDays(-1);
        var firstUtc = TimeZoneInfo.ConvertTimeToUtc(firstOfThisMonthLocal, Wib);
        var lastPrevUtc = TimeZoneInfo.ConvertTimeToUtc(lastOfPrevMonthLocal, Wib);

        var c = NewCustomer("Budi");
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        // Akhir bulan lalu 23:59 vs tanggal 1 00:00 WIB.
        db.TopupOrders.AddRange(
            PaidOrder(c.Id, 1, lastPrevUtc.AddSeconds(-1)),
            PaidOrder(c.Id, 2, firstUtc));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Month);

        var entry = Assert.Single(result);
        Assert.Equal(2m, entry.TotalSpent);
    }

    [Fact]
    public async Task GetLeaderboardAsync_WeekIncludesEarlierDaysOfSameWeek()
    {
        await using var db = CreateDb();
        var nowUtc = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Wib);
        var daysFromMonday = ((int)localNow.DayOfWeek + 6) % 7;
        // Awal minggu ini (Senin 00:00) masuk minggu — dan juga masuk "today"
        // hanya bila hari ini Senin; test ini hanya untuk periode Week.
        var mondayUtc = TimeZoneInfo.ConvertTimeToUtc(
            localNow.Date.AddDays(-daysFromMonday), Wib);

        var c = NewCustomer("Budi");
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        db.TopupOrders.Add(PaidOrder(c.Id, 2, mondayUtc));
        await db.SaveChangesAsync();

        var week = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Week);
        Assert.Single(week);
    }

    // ── Privasi & avatar ──

    [Fact]
    public async Task GetLeaderboardAsync_AvatarMapped_NullPreserved()
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var withAvatar = NewCustomer("Budi", "https://lh3.googleusercontent.com/pic");
        var without = NewCustomer("Sinta");
        db.Customers.AddRange(withAvatar, without);
        await db.SaveChangesAsync();
        db.TopupOrders.AddRange(
            PaidOrder(withAvatar.Id, 9000, start.AddMinutes(1)),
            PaidOrder(without.Id, 100, start.AddMinutes(2)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        Assert.Equal("https://lh3.googleusercontent.com/pic", result[0].AvatarUrl);
        Assert.Null(result[1].AvatarUrl);
    }

    [Theory]
    [InlineData("budi.santoso@gmail.com", "bud***")]
    [InlineData("si@x.test", "si***")]           // local part pendek: prefiks apa adanya
    [InlineData("Budi Santoso", "Budi Santoso")]  // nama biasa tidak disentuh
    public async Task GetLeaderboardAsync_EmailDerivedNameMasked(string fullName, string expected)
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var c = NewCustomer(fullName);
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        db.TopupOrders.Add(PaidOrder(c.Id, 5000, start.AddMinutes(1)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        Assert.Equal(expected, result[0].DisplayName);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ResponseShape_OnlyPublicSafeFields()
    {
        await using var db = CreateDb();
        var (start, _) = TodayWindow();
        var c = NewCustomer("Budi", avatarUrl: null, email: "secret@email.test");
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        db.TopupOrders.Add(PaidOrder(c.Id, 5000, start.AddMinutes(1)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetLeaderboardAsync(LeaderboardPeriod.Today);

        // DTO hanya boleh punya 4 properti publik-aman — tidak ada
        // Email/PasswordHash/CustomerId/OrderNo yang bocor.
        var props = typeof(LeaderboardEntryResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();
        Assert.Equal(new[] { "AvatarUrl", "DisplayName", "Rank", "TotalSpent" }, props);

        // Serialize penuh — pastikan email customer tidak muncul di mana pun.
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("secret@email.test", serialized);
        Assert.DoesNotContain("CustomerId", serialized);
        Assert.DoesNotContain("OrderNo", serialized);
    }

    // ── Parser periode (dipakai controller untuk validasi) ──

    [Theory]
    [InlineData(null, true, LeaderboardPeriod.Today)]     // tanpa param = default
    [InlineData("", true, LeaderboardPeriod.Today)]
    [InlineData("today", true, LeaderboardPeriod.Today)]
    [InlineData("TODAY", true, LeaderboardPeriod.Today)]  // case-insensitive
    [InlineData(" week ", true, LeaderboardPeriod.Week)]  // trim
    [InlineData("month", true, LeaderboardPeriod.Month)]
    [InlineData("yearly", false, default(LeaderboardPeriod))]
    [InlineData("yesterday", false, default(LeaderboardPeriod))]
    public void PeriodParser_ValidAndInvalidValues(string? input, bool expectedOk, LeaderboardPeriod expectedPeriod)
    {
        var ok = LeaderboardPeriodParser.TryParse(input, out var period);

        Assert.Equal(expectedOk, ok);
        Assert.Equal(expectedPeriod, period);
    }
}
