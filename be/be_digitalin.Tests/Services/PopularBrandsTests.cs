using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Models;
using be_digitalin.Providers;
using be_digitalin.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Menguji GetPopularGameBrandsAsync — section "Populer" di homepage yang
/// diurutkan otomatis dari penjualan, tanpa kurasi manual. Yang dijaga:
/// hanya order COMPLETED dalam 30 hari yang dihitung, urutannya turun,
/// brand tanpa produk aktif gugur, dan jumlahnya dibatasi ukuran grid.
/// </summary>
public class PopularBrandsTests
{
    [Fact]
    public async Task Popular_RanksByCompletedSales_Descending()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Free Fire", activeProduct: true);
        SeedBrand(db, "Mobile Legends", activeProduct: true);
        SeedBrand(db, "Valorant", activeProduct: true);
        SeedOrders(db, "Mobile Legends", 3);
        SeedOrders(db, "Free Fire", 5);
        SeedOrders(db, "Valorant", 1);

        var result = (await CreateService(db).GetPopularGameBrandsAsync()).ToList();

        result.Select(brand => brand.Brand).Should()
            .Equal("Free Fire", "Mobile Legends", "Valorant");
        result[0].Slug.Should().Be("free-fire");
    }

    [Fact]
    public async Task Popular_CountsOnlyCompletedOrders()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Mobile Legends", activeProduct: true);
        SeedBrand(db, "Valorant", activeProduct: true);
        SeedOrders(db, "Mobile Legends", 1);
        SeedOrders(db, "Valorant", 4, status: AppConstants.OrderStatus.Pending);
        SeedOrders(db, "Valorant", 4, status: AppConstants.OrderStatus.Expired);

        var result = (await CreateService(db).GetPopularGameBrandsAsync()).ToList();

        result.Select(brand => brand.Brand).Should().Equal("Mobile Legends");
    }

    [Fact]
    public async Task Popular_IgnoresOrdersOutsideWindow()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Mobile Legends", activeProduct: true);
        SeedBrand(db, "Valorant", activeProduct: true);
        SeedOrders(db, "Mobile Legends", 1);
        // Laris tahun lalu tidak berarti populer sekarang.
        SeedOrders(db, "Valorant", 9, daysAgo: 45);

        var result = (await CreateService(db).GetPopularGameBrandsAsync()).ToList();

        result.Select(brand => brand.Brand).Should().Equal("Mobile Legends");
    }

    [Fact]
    public async Task Popular_ExcludesBrandWithoutActiveProduct()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Sudah Ditutup", activeProduct: false);
        SeedBrand(db, "Mobile Legends", activeProduct: true);
        SeedOrders(db, "Sudah Ditutup", 10);
        SeedOrders(db, "Mobile Legends", 1);

        var result = (await CreateService(db).GetPopularGameBrandsAsync()).ToList();

        // Kartu brand mati mengarah ke halaman detail kosong.
        result.Select(brand => brand.Brand).Should().Equal("Mobile Legends");
    }

    [Fact]
    public async Task Popular_BrandWithoutMetadata_DoesNotAppear()
    {
        await using var db = CreateDbContext();
        SeedOrders(db, "Brand Hantu", 10);

        var result = await CreateService(db).GetPopularGameBrandsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Popular_NoSales_ReturnsEmpty()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Mobile Legends", activeProduct: true);

        var result = await CreateService(db).GetPopularGameBrandsAsync();

        // Toko baru: FE menyembunyikan section-nya, bukan menampilkan judul kosong.
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Popular_CapsAtGridSize()
    {
        await using var db = CreateDbContext();

        // 15 brand, penjualan menurun: "Game 00" terlaris.
        for (var i = 0; i < 15; i++)
        {
            var brand = $"Game {i:00}";
            SeedBrand(db, brand, activeProduct: true);
            SeedOrders(db, brand, 15 - i);
        }

        var result = (await CreateService(db).GetPopularGameBrandsAsync()).ToList();

        result.Should().HaveCount(12);
        result.First().Brand.Should().Be("Game 00");
        result.Last().Brand.Should().Be("Game 11");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static DigitalinDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DigitalinDbContext(options);
    }

    private static ProductService CreateService(DigitalinDbContext db)
    {
        // Provider eksternal tidak disentuh query ini; cukup settings kosong.
        var r2Provider = new CloudflareR2Provider(
            Options.Create(new CloudflareR2Settings
            {
                AccountId = "test-account"
            }));
        var digiflazzProvider = new DigiflazzProvider(
            new HttpClient(),
            Options.Create(new DigiflazzSettings()));

        return new ProductService(db, r2Provider, digiflazzProvider);
    }

    private static void SeedBrand(
        DigitalinDbContext db,
        string brand,
        bool activeProduct)
    {
        db.GameMetadata.Add(new GameMetadata { Brand = brand });

        db.GameProducts.Add(new GameProduct
        {
            Name = $"{brand} 100 Diamonds",
            Price = 10_000m,
            GameCode = brand,
            Brand = brand,
            Category = AppConstants.Categories.Games,
            IsActive = activeProduct
        });

        db.SaveChanges();
    }

    private static void SeedOrders(
        DigitalinDbContext db,
        string brand,
        int count,
        string? status = null,
        int daysAgo = 1)
    {
        for (var i = 0; i < count; i++)
        {
            db.TopupOrders.Add(new TopupOrder
            {
                OrderNo = Guid.NewGuid().ToString("N"),
                Status = status ?? AppConstants.OrderStatus.Completed,
                OrderDate = DateTime.UtcNow.AddDays(daysAgo * -1),
                BrandName = brand,
                CategoryName = AppConstants.Categories.Games,
                ProductName = $"{brand} 100 Diamonds",
                TotalPaid = 10_000m
            });
        }

        db.SaveChanges();
    }
}
