using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using be_digitalin.Services.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Unit test GetRecentGameBrandsAsync (section "Terakhir Dibeli") dengan EF
/// InMemory. Menutup: user login dengan riwayat (terbaru dulu, tanpa
/// duplikat, hanya status uang-masuk), user login tanpa riwayat (kosong),
/// guest slug cookie (urutan dipertahankan, slug tidak dikenal dibuang,
/// input jahat dinormalisasi, batas jumlah), dan brand nonaktif yang hilang.
/// </summary>
public class ProductServiceRecentGamesTests
{
    private static DigitalinDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DigitalinDbContext(options);
    }

    /// <summary>
    /// Service hanya butuh _db untuk method ini — provider lain tidak disentuh
    /// (tidak ada cache Redis: hasil per-customer/cookie).
    /// </summary>
    private static ProductService CreateService(DigitalinDbContext db) =>
        new(db, null!, null!, null!,
            NullLogger<ProductService>.Instance, null!);

    private static void SeedCatalog(
        DigitalinDbContext db,
        params (string Brand, string Slug, bool Active)[]
        brands)
    {
        foreach (var (brand, slug, active) in brands)
        {
            db.GameMetadata.Add(new GameMetadata
            {
                Brand = brand,
                BrandSlug = slug,
                Categories = "mobile"
            });
            db.GameProducts.Add(new GameProduct
            {
                Name = $"{brand} 100",
                Brand = brand,
                Category = AppConstants.Categories.Games,
                BuyerSkuCode = $"sku-{slug}",
                IsActive = active,
                Price = 10000
            });
        }
    }

    private static TopupOrder NewPaidOrder(
        string brand,
        int? customerId,
        DateTime orderDate,
        string status = AppConstants.OrderStatus.Completed) =>
        new()
        {
            OrderNo = $"INV-{orderDate:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..34],
            PlayerId = "player",
            Status = status,
            OrderDate = orderDate,
            CustomerId = customerId,
            GameProductId = 1,
            ProductName = "100 diamond",
            BuyerSkuCode = "sku",
            BrandName = brand,
            CategoryName = AppConstants.Categories.Games,
            TotalPaid = 10000
        };

    // ── User login: DB = sumber kebenaran ──

    [Fact]
    public async Task Authenticated_WithHistory_MostRecentFirstNoDuplicates()
    {
        await using var db = CreateDb();
        SeedCatalog(db,
            ("MOBILE LEGENDS", "mobile-legends", true),
            ("FREE FIRE", "free-fire", true),
            ("GENSHIN IMPACT", "genshin-impact", true));
        // Dua order GENSHIN (terbaru) lalu satu ML — GENSHIN harus tampil
        // duluan meski dibeli dua kali (duplikat dihitung sekali).
        db.TopupOrders.AddRange(
            NewPaidOrder("FREE FIRE", 7, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)),
            NewPaidOrder("GENSHIN IMPACT", 7, new DateTime(2026, 9, 8, 8, 0, 0, DateTimeKind.Utc)),
            NewPaidOrder("GENSHIN IMPACT", 7, new DateTime(2026, 9, 9, 8, 0, 0, DateTimeKind.Utc)),
            NewPaidOrder("MOBILE LEGENDS", 7, new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var result = (await CreateService(db).GetRecentGameBrandsAsync(customerId: 7)).ToList();

        Assert.Equal(
            ["genshin-impact", "mobile-legends", "free-fire"],
            result.Select(brand => brand.Slug));
    }

    [Fact]
    public async Task Authenticated_WithoutHistory_ReturnsEmpty()
    {
        await using var db = CreateDb();
        SeedCatalog(db, ("MOBILE LEGENDS", "mobile-legends", true));
        // Riwayat milik customer lain — tidak boleh bocor.
        db.TopupOrders.Add(NewPaidOrder("MOBILE LEGENDS", 99,
            new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetRecentGameBrandsAsync(customerId: 7);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(AppConstants.OrderStatus.Pending)]
    [InlineData(AppConstants.OrderStatus.Expired)]
    [InlineData(AppConstants.OrderStatus.Failed)]
    public async Task Authenticated_NonPaidStatuses_Excluded(string status)
    {
        await using var db = CreateDb();
        SeedCatalog(db, ("MOBILE LEGENDS", "mobile-legends", true));
        db.TopupOrders.Add(NewPaidOrder("MOBILE LEGENDS", 7,
            new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc), status));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetRecentGameBrandsAsync(customerId: 7);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Processing)]
    [InlineData(AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.NeedsAction)]
    public async Task Authenticated_PaidStatuses_Included(string status)
    {
        await using var db = CreateDb();
        SeedCatalog(db, ("MOBILE LEGENDS", "mobile-legends", true));
        db.TopupOrders.Add(NewPaidOrder("MOBILE LEGENDS", 7,
            new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc), status));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetRecentGameBrandsAsync(customerId: 7);

        var brand = Assert.Single(result);
        Assert.Equal("mobile-legends", brand.Slug);
    }

    [Fact]
    public async Task Authenticated_CappedAtLimit()
    {
        await using var db = CreateDb();
        for (var i = 0; i < ProductService.RecentBrandLimit + 5; i++)
        {
            var brand = $"GAME {i}";
            SeedCatalog(db, (brand, $"game-{i}", true));
            db.TopupOrders.Add(NewPaidOrder(brand, 7,
                new DateTime(2026, 9, 1, 8, 0, i, DateTimeKind.Utc)));
        }
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetRecentGameBrandsAsync(customerId: 7);

        Assert.Equal(ProductService.RecentBrandLimit, result.Count());
    }

    [Fact]
    public async Task Authenticated_InactiveBrand_Dropped()
    {
        await using var db = CreateDb();
        SeedCatalog(db,
            ("MOBILE LEGENDS", "mobile-legends", true),
            ("FREE FIRE", "free-fire", false));
        db.TopupOrders.AddRange(
            NewPaidOrder("MOBILE LEGENDS", 7, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)),
            NewPaidOrder("FREE FIRE", 7, new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetRecentGameBrandsAsync(customerId: 7);

        var brand = Assert.Single(result);
        Assert.Equal("mobile-legends", brand.Slug);
    }

    // ── Guest: cookie slug = hint urutan, divalidasi vs katalog aktif ──

    [Fact]
    public async Task Guest_Slugs_PreserveCookieOrder()
    {
        await using var db = CreateDb();
        SeedCatalog(db,
            ("MOBILE LEGENDS", "mobile-legends", true),
            ("FREE FIRE", "free-fire", true),
            ("GENSHIN IMPACT", "genshin-impact", true));
        await db.SaveChangesAsync();

        var result = (await CreateService(db).GetRecentGameBrandsAsync(
            customerId: null,
            guestSlugs: ["free-fire", "mobile-legends", "genshin-impact"])).ToList();

        Assert.Equal(
            ["free-fire", "mobile-legends", "genshin-impact"],
            result.Select(brand => brand.Slug));
    }

    [Fact]
    public async Task Guest_UnknownSlugs_Ignored()
    {
        await using var db = CreateDb();
        SeedCatalog(db, ("MOBILE LEGENDS", "mobile-legends", true));

        await db.SaveChangesAsync();
        var result = (await CreateService(db).GetRecentGameBrandsAsync(
            customerId: null,
            guestSlugs: ["tidak-ada", "mobile-legends", "game-hantu"])).ToList();

        var brand = Assert.Single(result);
        Assert.Equal("mobile-legends", brand.Slug);
    }

    [Fact]
    public async Task Guest_TamperedSlugs_NormalizedOrDropped()
    {
        await using var db = CreateDb();
        SeedCatalog(db, ("MOBILE LEGENDS", "mobile-legends", true));

        // Karakter aneh dinormalisasi Slug.From ("Mobile Legends!!!" →
        // "mobile-legends"); sampah murni hilang; null/empty aman dilewati.
        await db.SaveChangesAsync();
        var result = (await CreateService(db).GetRecentGameBrandsAsync(
            customerId: null,
            guestSlugs: ["  Mobile Legends!!!  ", "***", "", "mobile-legends"])).ToList();

        var brand = Assert.Single(result);
        Assert.Equal("mobile-legends", brand.Slug);
    }

    [Fact]
    public async Task Guest_OversizedSlugs_CappedAtLimit()
    {
        await using var db = CreateDb();
        var expected = new List<string>();
        for (var i = 0; i < ProductService.RecentBrandLimit + 5; i++)
        {
            SeedCatalog(db, ($"GAME {i}", $"game-{i}", true));
            expected.Add($"game-{i}");
        }
        await db.SaveChangesAsync();

        var result = (await CreateService(db).GetRecentGameBrandsAsync(
            customerId: null,
            guestSlugs: expected)).ToList();

        Assert.Equal(ProductService.RecentBrandLimit, result.Count);
        Assert.Equal(expected.Take(ProductService.RecentBrandLimit),
            result.Select(brand => brand.Slug));
    }

    [Fact]
    public async Task Guest_NoSlugs_ReturnsEmpty()
    {
        await using var db = CreateDb();
        SeedCatalog(db, ("MOBILE LEGENDS", "mobile-legends", true));

        var noSlugs = await CreateService(db).GetRecentGameBrandsAsync(customerId: null);
        var emptySlugs = await CreateService(db).GetRecentGameBrandsAsync(
            customerId: null, guestSlugs: []);

        Assert.Empty(noSlugs);
        Assert.Empty(emptySlugs);
    }

    [Fact]
    public async Task Guest_SlugsNeverExposeOrderData()
    {
        await using var db = CreateDb();
        SeedCatalog(db, ("MOBILE LEGENDS", "mobile-legends", true));
        // Order privat milik customer lain ada di DB — slug guest tidak boleh
        // pernah bisa mengintipnya; respons hanya CategoryInfo katalog publik.
        db.TopupOrders.Add(NewPaidOrder("MOBILE LEGENDS", 99,
            new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetRecentGameBrandsAsync(
            customerId: null, guestSlugs: ["mobile-legends"]);

        var brand = Assert.Single(result);
        var serialized = System.Text.Json.JsonSerializer.Serialize(brand);
        Assert.DoesNotContain("CustomerId", serialized);
        Assert.DoesNotContain("OrderNo", serialized);
        Assert.DoesNotContain("player", serialized);
    }
}
