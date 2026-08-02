using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Providers;
using be_digitalin.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Menguji fitur kategori storefront di ProductService:
/// NormalizeCategory/NormalizeCategories (helper murni, input admin → slug
/// kebab-case, multi-kategori → CSV), GetGameCategoriesAsync (agregasi
/// brandCount + nama tampilan + bucket virtual all/lainnya), PageBrands
/// (filter kategori + potong halaman), serta aliran Categories pada
/// GetUniqueGameBrandsAsync & UpdateGameMetadataAsync.
/// Test DB memakai EF InMemory; provider eksternal tidak disentuh query ini
/// sehingga cukup diinstansiasi dengan settings kosong.
/// </summary>
public class GameCategoryTests
{
    // ── NormalizeCategory (helper murni) ──────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeCategory_EmptyInput_ReturnsNull(string? input)
    {
        ProductService.NormalizeCategory(input).Should().BeNull();
    }

    [Theory]
    [InlineData("mobile", "mobile")]
    [InlineData("Mobile", "mobile")]
    [InlineData("Mobile Game", "mobile-game")]
    [InlineData("  PC   Game  ", "pc-game")]
    [InlineData("pulsa-data", "pulsa-data")]
    // Karakter non-alfanumerik jadi pemisah: slug harus aman dipakai di URL
    // dan sebagai elemen CSV di GameMetadata.Categories (koma tidak boleh lolos).
    [InlineData("Konsol, Game", "konsol-game")]
    [InlineData("E-Money", "e-money")]
    [InlineData("-- ??? --", null)]
    public void NormalizeCategory_ValidInput_ReturnsKebabCaseSlug(
        string input, string? expected)
    {
        ProductService.NormalizeCategory(input).Should().Be(expected);
    }

    // ── NormalizeCategories (multi-kategori → CSV) ────────────────

    [Fact]
    public void NormalizeCategories_NormalizesDedupesAndKeepsOrder()
    {
        ProductService.NormalizeCategories(["PC Game", "mobile", "PC   Game", "  "])
            .Should().Be("pc-game,mobile");

        ProductService.NormalizeCategories([]).Should().BeNull();
        ProductService.NormalizeCategories(null).Should().BeNull();
    }

    // ── GetGameCategoriesAsync ────────────────────────────────────

    [Fact]
    public async Task Categories_AggregateBrandCount_PerCategory()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Mobile Legends", "mobile", activeProduct: true);
        SeedBrand(db, "Free Fire", "mobile", activeProduct: true);
        SeedBrand(db, "Valorant", "pc", activeProduct: true);

        var service = CreateService(db);

        var result = await service.GetGameCategoriesAsync();

        // "all" ikut dikirim: FE memakainya sebagai total section "Semua",
        // sekarang brand-nya diambil dipaginasi jadi FE tak bisa menghitung sendiri.
        result.Should().BeEquivalentTo(
        [
            new GameCategoryInfo("all", "Semua", 3),
            new GameCategoryInfo("mobile", "Mobile Game", 2),
            new GameCategoryInfo("pc", "PC Game", 1)
        ]);
    }

    [Fact]
    public async Task Categories_NullCategory_GoesToUncategorizedBucket_SkipsInactiveBrand()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Tanpa Kategori", null, activeProduct: true);
        SeedBrand(db, "Produk Nonaktif", "voucher", activeProduct: false);

        var service = CreateService(db);

        var result = await service.GetGameCategoriesAsync();

        // Brand tanpa kategori tetap punya section ("lainnya"); brand tanpa produk
        // aktif tidak dihitung di mana pun — termasuk di "all".
        result.Should().BeEquivalentTo(
        [
            new GameCategoryInfo("all", "Semua", 1),
            new GameCategoryInfo("lainnya", "Lainnya", 1)
        ]);
    }

    [Fact]
    public async Task Categories_EmptyCatalog_ReturnsNothing()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Produk Nonaktif", "mobile", activeProduct: false);

        var service = CreateService(db);

        // Tanpa satu pun brand aktif, tab "Semua 0" tidak berguna.
        (await service.GetGameCategoriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Categories_UnknownSlug_FallsBackToSlugAsName()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Arcade Heroes", "arcade", activeProduct: true);

        var service = CreateService(db);

        var result = await service.GetGameCategoriesAsync();

        result.Should().Contain(new GameCategoryInfo("arcade", "arcade", 1));
    }

    [Fact]
    public async Task Categories_MultiCategoryGame_CountsInEveryCategory()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Genshin Impact", "mobile,pc", activeProduct: true);
        SeedBrand(db, "Valorant", "pc", activeProduct: true);

        var service = CreateService(db);

        // Game multi-kategori dihitung di tiap kategorinya, jadi total brandCount
        // (1 + 2) boleh lebih besar dari jumlah brand — tapi "all" tetap 2.
        (await service.GetGameCategoriesAsync()).Should().BeEquivalentTo(
        [
            new GameCategoryInfo("all", "Semua", 2),
            new GameCategoryInfo("mobile", "Mobile Game", 1),
            new GameCategoryInfo("pc", "PC Game", 2)
        ]);
    }

    // ── GetUniqueGameBrandsAsync: categorySlugs ikut terkirim ─────

    [Fact]
    public async Task Brands_IncludeCategorySlugs_FromMetadata()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Genshin Impact", "mobile,pc", activeProduct: true);
        SeedBrand(db, "Tanpa Kategori", null, activeProduct: true);

        var service = CreateService(db);

        var result = (await service.GetUniqueGameBrandsAsync()).ToList();

        result.Should().HaveCount(2);
        result.Single(brand => brand.Slug == "genshin-impact")
            .CategorySlugs.Should().Equal("mobile", "pc");
        result.Single(brand => brand.Slug == "tanpa-kategori")
            .CategorySlugs.Should().BeEmpty();
    }

    // ── PageBrands: filter kategori + potong halaman (fungsi murni) ──

    [Theory]
    // Tanpa kategori & bucket "all" = seluruh katalog, tanpa filter.
    [InlineData(null, "mobile-legends,tanpa-kategori,valorant")]
    [InlineData("all", "mobile-legends,tanpa-kategori,valorant")]
    [InlineData("mobile", "mobile-legends")]
    [InlineData("pc", "valorant")]
    // Bucket virtual: hanya brand yang belum dikategorikan admin.
    [InlineData("lainnya", "tanpa-kategori")]
    [InlineData("Lainnya", "tanpa-kategori")]
    // Kategori yang tidak dipakai brand mana pun → section kosong, bukan seluruh katalog.
    [InlineData("voucher", "")]
    public void PageBrands_FiltersByCategorySlug(string? category, string expected)
    {
        ProductService.PageBrands(SampleBrands(), category, offset: 0, limit: null)
            .Select(brand => brand.Slug)
            .Should().Equal(expected.Length == 0 ? [] : expected.Split(','));
    }

    [Fact]
    public void PageBrands_OffsetAndLimit_SliceWithoutSkippingOrRepeating()
    {
        var all = SampleBrands();

        // Batch pertama kecil, batch berikutnya lanjut dari offset = jumlah termuat:
        // inilah alasan endpoint memakai offset, bukan nomor halaman.
        ProductService.PageBrands(all, null, offset: 0, limit: 2)
            .Select(brand => brand.Slug)
            .Should().Equal("mobile-legends", "tanpa-kategori");

        ProductService.PageBrands(all, null, offset: 2, limit: 2)
            .Select(brand => brand.Slug)
            .Should().Equal("valorant");

        // Offset melewati ujung daftar = kosong, bukan error.
        ProductService.PageBrands(all, null, offset: 99, limit: 2).Should().BeEmpty();
    }

    [Fact]
    public async Task Brands_PagedPerCategory_MatchesCategoryBrandCount()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Mobile Legends", "mobile", activeProduct: true);
        SeedBrand(db, "Free Fire", "mobile", activeProduct: true);
        SeedBrand(db, "Valorant", "pc", activeProduct: true);

        var service = CreateService(db);

        // Total tiap section (brandCount di /games/categories) harus persis sama
        // dengan jumlah brand yang bisa diambil section itu lewat offset/limit —
        // kalau tidak, tombol "Tampilkan lebih banyak" ikut salah.
        var categories = (await service.GetGameCategoriesAsync()).ToList();

        foreach (var category in categories)
        {
            var loaded = new List<string>();

            while (true)
            {
                var batch = (await service.GetUniqueGameBrandsAsync(
                    category.Slug, loaded.Count, limit: 2)).ToList();

                if (batch.Count == 0) break;
                loaded.AddRange(batch.Select(brand => brand.Slug));
            }

            loaded.Should().HaveCount(category.BrandCount, $"section {category.Slug}");
            loaded.Should().OnlyHaveUniqueItems();
        }
    }

    // ── UpdateGameMetadataAsync: kategori ternormalisasi & tersimpan ──

    [Fact]
    public async Task UpdateMetadata_SavesNormalizedCategories()
    {
        await using var db = CreateDbContext();
        var metadata = new GameMetadata { Brand = "Genshin Impact" };
        db.GameMetadata.Add(metadata);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var request = new UpdateGameMetadataRequest(
            [Field("userId")],
            "{userId}",
            ["Mobile Game", "PC Game"]);

        var response = await service.UpdateGameMetadataAsync(metadata.Id, request);

        response.Should().NotBeNull();
        response!.Categories.Should().Equal("mobile-game", "pc-game");
        metadata.Categories.Should().Be("mobile-game,pc-game");
    }

    [Fact]
    public async Task UpdateMetadata_EmptyCategories_ClearsToNull()
    {
        await using var db = CreateDbContext();
        var metadata = new GameMetadata
        {
            Brand = "Mobile Legends",
            Categories = "mobile,pc"
        };
        db.GameMetadata.Add(metadata);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var request = new UpdateGameMetadataRequest(
            [Field("userId")],
            "{userId}",
            ["   "]);

        var response = await service.UpdateGameMetadataAsync(metadata.Id, request);

        response.Should().NotBeNull();
        response!.Categories.Should().BeEmpty();
        metadata.Categories.Should().BeNull();
    }

    // ── Registry kategori (urutan & custom category dari admin) ──

    [Fact]
    public void Registry_Parse_CorruptJson_FallsBackToDefaults()
    {
        GameCategoryRegistry.Parse("{bukan json}")
            .Should().BeEquivalentTo(GameCategoryRegistry.Defaults());

        GameCategoryRegistry.Parse(null)
            .Should().BeEquivalentTo(GameCategoryRegistry.Defaults());
    }

    [Fact]
    public void Registry_Validate_PreservesOrder_AndDerivesSlugForNewCategory()
    {
        var entries = GameCategoryRegistry.Validate(
        [
            new GameCategoryEntryRequest("e-money", "E-Money"),
            new GameCategoryEntryRequest("mobile", "Game Mobile"),   // rename, slug tetap
            new GameCategoryEntryRequest(null, "  Konsol  Game  ")   // custom baru
        ]);

        entries.Select(entry => entry.Slug).Should()
            .Equal("e-money", "mobile", "konsol-game");
        entries[1].Name.Should().Be("Game Mobile");
        entries[2].Name.Should().Be("Konsol  Game");
    }

    [Theory]
    [InlineData("all")]
    [InlineData("Lainnya")]
    [InlineData("populer")]
    public void Registry_Validate_RejectsReservedSlug(string slug)
    {
        var act = () => GameCategoryRegistry.Validate(
            [new GameCategoryEntryRequest(slug, "Apa Saja")]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Registry_Validate_RejectsDuplicateSlug_AndEmptyName()
    {
        var duplicate = () => GameCategoryRegistry.Validate(
        [
            new GameCategoryEntryRequest("mobile", "Mobile Game"),
            new GameCategoryEntryRequest("Mobile", "Game HP")   // slug sama, beda casing
        ]);
        duplicate.Should().Throw<InvalidOperationException>();

        var emptyName = () => GameCategoryRegistry.Validate(
            [new GameCategoryEntryRequest("mobile", "   ")]);
        emptyName.Should().Throw<InvalidOperationException>();

        var emptyList = () => GameCategoryRegistry.Validate([]);
        emptyList.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Registry_Update_ChangesStorefrontOrder()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Mobile Legends", "mobile", activeProduct: true);
        SeedBrand(db, "Dana", "e-money", activeProduct: true);

        var service = CreateService(db);

        // Default: mobile sebelum e-money. "all" selalu tab pertama.
        (await service.GetGameCategoriesAsync())
            .Select(category => category.Slug)
            .Should().Equal("all", "mobile", "e-money");

        await service.UpdateGameCategoryRegistryAsync(new UpdateGameCategoriesRequest(
        [
            new GameCategoryEntryRequest("e-money", "E-Money"),
            new GameCategoryEntryRequest("mobile", "Mobile Game")
        ]));

        (await service.GetGameCategoriesAsync())
            .Select(category => category.Slug)
            .Should().Equal("all", "e-money", "mobile");
    }

    [Fact]
    public async Task Registry_Admin_IncludesEmptyCustomCategory()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Mobile Legends", "mobile", activeProduct: true);

        var service = CreateService(db);

        await service.UpdateGameCategoryRegistryAsync(new UpdateGameCategoriesRequest(
        [
            new GameCategoryEntryRequest("mobile", "Mobile Game"),
            new GameCategoryEntryRequest(null, "Konsol")
        ]));

        // Admin melihat kategori kosong (supaya bisa dipilih di editor metadata),
        // storefront tidak (section berjudul tanpa kartu).
        (await service.GetGameCategoryRegistryAsync())
            .Should().ContainEquivalentOf(new GameCategoryInfo("konsol", "Konsol", 0));

        (await service.GetGameCategoriesAsync())
            .Select(category => category.Slug)
            .Should().Equal("all", "mobile");
    }

    [Fact]
    public async Task Registry_Update_RejectsDeletingCategoryStillUsedByGame()
    {
        await using var db = CreateDbContext();
        // "pc" hanya kategori kedua game ini — guard harus tetap melihatnya.
        SeedBrand(db, "Genshin Impact", "mobile,pc", activeProduct: true);

        var service = CreateService(db);

        var act = async () => await service.UpdateGameCategoryRegistryAsync(
            new UpdateGameCategoriesRequest(
                [new GameCategoryEntryRequest("mobile", "Mobile Game")]));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*pc*");
    }

    [Fact]
    public async Task Registry_UnknownSlugFromMetadata_StaysVisible()
    {
        await using var db = CreateDbContext();
        SeedBrand(db, "Arcade Heroes", "arcade", activeProduct: true);

        var service = CreateService(db);

        // "arcade" tidak ada di registry, tapi brand-nya tidak boleh hilang
        // dari homepage — dan admin harus melihatnya untuk bisa mengadopsi.
        (await service.GetGameCategoryRegistryAsync())
            .Should().ContainEquivalentOf(new GameCategoryInfo("arcade", "arcade", 1));
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Katalog kecil terurut brand (seperti keluaran query GetUniqueGameBrandsAsync)
    /// untuk menguji PageBrands tanpa DB.
    /// </summary>
    private static List<CategoryInfo> SampleBrands() =>
    [
        new("Mobile Legends", "mobile-legends", null, AppConstants.Categories.Games, ["mobile"]),
        new("Tanpa Kategori", "tanpa-kategori", null, AppConstants.Categories.Games, []),
        new("Valorant", "valorant", null, AppConstants.Categories.Games, ["pc"])
    ];

    private static DigitalinDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DigitalinDbContext(options);
    }

    private static ProductService CreateService(DigitalinDbContext db)
    {
        // AccountId dummy: konstruktor memvalidasi format ServiceURL S3.
        // Client tidak melakukan network call selama test (query tak menyentuh R2).
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
        string? categories,
        bool activeProduct)
    {
        db.GameMetadata.Add(new GameMetadata
        {
            Brand = brand,
            // CSV, seperti tersimpan di kolom: "mobile" atau "mobile,pc".
            Categories = categories
        });

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

    private static GameInputField Field(string key) => new()
    {
        Key = key,
        Label = key,
        Type = AppConstants.GameInputFieldTypes.Text,
        Required = true,
        Order = 1
    };
}
