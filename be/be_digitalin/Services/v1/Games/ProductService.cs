using be_digitalin.Application.Ports;
using be_digitalin.DTOs;
using be_digitalin.Domain.Entities;
using be_digitalin.Constants;
using be_digitalin.Domain;

using be_digitalin.Application.Serialization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using be_digitalin.Application.Validation;
using be_digitalin.Domain.Games.GameTemplates;
using be_digitalin.Domain.Orders;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Settings;

namespace be_digitalin.Services.Games;

public class ProductService : IProductService
{
    private readonly IApplicationDbContext _db;
    private readonly ICloudflareR2Provider _r2Provider;
    private readonly IDigiflazzProvider _digiflazzProvider;
    private readonly IAppSettingsService _appSettings;
    private readonly ILogger<ProductService> _logger;
    private readonly IRedisService _redis;

    /// <summary>
    /// Jumlah game di section "Populer". Sama dengan batas grid homepage,
    /// jadi section-nya penuh tanpa tombol "Tampilkan lebih banyak".
    /// </summary>
    private const int PopularBrandLimit = 12;

    // TTL dasar + jitter kecil supaya expiration banyak key tidak serentak.
    private static TimeSpan CacheTtl(int minutes) =>
        TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(Random.Shared.Next(0, 31)));

    public ProductService(
        IApplicationDbContext db,
        ICloudflareR2Provider r2Provider,
        IDigiflazzProvider digiflazzProvider,
        IAppSettingsService appSettings,
        ILogger<ProductService> logger,
        IRedisService redis)
    {
        _db = db;
        _r2Provider = r2Provider;
        _digiflazzProvider = digiflazzProvider;
        _appSettings = appSettings;
        _logger = logger;
        _redis = redis;
    }
    
    public async Task<IReadOnlyList<GameMetadataResponse>>
        GetGameMetadataAsync()
    {
        return await TryGetGameMetadataListAsync();
    }

    public async Task<GameMetadataResponse?>
        GetGameMetadataByIdAsync(int id)
    {
        var list = await TryGetGameMetadataListAsync();

        return list.FirstOrDefault(m => m.Id == id);
    }

    public async Task<GameMetadataResponse?>
        UpdateGameMetadataAsync(
            int id,
            UpdateGameMetadataRequest request)
    {
        var metadata = await _db.GameMetadata
            .FirstOrDefaultAsync(m => m.Id == id);

        if (metadata == null)
            return null;

        var normalizedFields = NormalizeAndValidateInputFields(
            request.InputFields);

        var normalizedTargetTemplate = ValidateTargetTemplate(
            request.TargetTemplate,
            normalizedFields);

        metadata.InputSchemaJson = GameInputSchemaSerializer.Serialize(normalizedFields);
        metadata.TargetTemplate = normalizedTargetTemplate;
        metadata.Categories = NormalizeCategories(request.Categories);
        metadata.UsernameCheckProductId = await ValidateUsernameCheckProductAsync(
            metadata.Brand,
            request.UsernameCheckProductId);

        await _db.SaveChangesAsync();

        // DB sudah sukses — schema/template berubah, cache metadata dibuang
        // agar order path tidak memakai konfigurasi lama (best-effort).
        await TryInvalidateCacheAsync(RedisCacheKeys.GameMetadata);

        return MapMetadataResponse(metadata);
    }

    /// <summary>
    /// Normalisasi kategori storefront dari input admin menjadi slug kebab-case
    /// lewat <see cref="Slug"/> ("E-Money" → "e-money", "Konsol, Game" →
    /// "konsol-game"). Input kosong/whitespace → null. Hanya menyisakan
    /// [a-z0-9-] supaya slug aman dipakai di URL/anchor FE dan sebagai elemen
    /// CSV di GameMetadata.Categories.
    /// </summary>
    internal static string? NormalizeCategory(string? category) =>
        Slug.From(category);

    /// <summary>
    /// Beberapa kategori sekaligus → CSV siap simpan. Tiap entri dinormalisasi,
    /// yang kosong dibuang, duplikat (beda casing sekalipun) dibuang, urutan
    /// pilihan admin dipertahankan. Daftar kosong/null → null.
    /// </summary>
    internal static string? NormalizeCategories(IEnumerable<string>? categories)
    {
        if (categories == null)
            return null;

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slugs = new List<string>();

        foreach (var category in categories)
        {
            var slug = NormalizeCategory(category);

            if (slug != null && unique.Add(slug))
                slugs.Add(slug);
        }

        return GameMetadata.JoinCategories(slugs);
    }

    // logic sync
    public async Task<(int Added, int Updated, int Deactivated)> SyncCatalogFromDigiflazzAsync(CancellationToken cancellationToken = default)
    {
        var dataArray = await _digiflazzProvider.GetPriceListAsync(cancellationToken);

        int added = 0;
        int updated = 0;
        int deactivated = 0;

        var activeDigiflazzSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Load katalog sekali ke memori (hindari N+1 di dalam loop) ──
        // Entity-nya dibawa (bukan cuma nama brand) karena SupplierNote ditulis
        // ke metadata brand yang sudah ada, bukan hanya brand baru.
        // Brand di-lookup case-insensitive agar sepadan dengan kolasi default MySQL.
        var metadataByBrand = (await _db.GameMetadata.ToListAsync())
            .GroupBy(m => m.Brand, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // SKU unik → lookup per item cukup dari dictionary (bukan query).
        var products = await _db.GameProducts.ToListAsync();
        var productsBySku = products
            .Where(p => !string.IsNullOrEmpty(p.BuyerSkuCode))
            .GroupBy(p => p.BuyerSkuCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in dataArray)
        {
            var category = item.Category;

            if (category != AppConstants.Categories.Games)
                continue;

            var sku = item.BuyerSkuCode;
            var name = item.ProductName;
            var modalPrice = item.Price;
            var brand = item.Brand;

            if (string.IsNullOrWhiteSpace(sku))
                continue;

            // Brand tanpa huruf/angka tidak punya slug, jadi tidak punya URL
            // detail — produknya tidak akan pernah bisa dibuka dari storefront.
            var brandSlug = Slug.From(brand);

            if (brandSlug == null)
                continue;

            // Produk hanya boleh dijual kalau supplier DAN seller sama-sama
            // menyalakannya. Salah satu false = katalog kita ikut mematikannya,
            // supaya order tidak lolos checkout lalu ditolak Digiflazz setelah
            // pembayaran masuk.
            var isSellable = item.BuyerProductStatus && item.SellerProductStatus;

            activeDigiflazzSkus.Add(sku);

            if (!metadataByBrand.TryGetValue(brand, out var metadata))
            {
                metadata = new GameMetadata
                {
                    Brand = brand,
                    BrandSlug = brandSlug,
                    InputSchemaJson = GameInputSchemaSerializer.Serialize(
                        GameTemplateFactory.CreateDefaultInputFields()),
                    TargetTemplate = GameTemplateFactory.DefaultTargetTemplate
                };

                _db.GameMetadata.Add(metadata);

                // Tandai agar SKU lain dengan brand sama tidak menambah metadata duplikat.
                metadataByBrand[brand] = metadata;
            }

            // Slug ikut brand: kalau aturan slug diperketat atau brand berubah
            // nama, baris lama disegarkan di sync berikutnya — bukan dihitung
            // ulang tiap query.
            if (metadata.BrandSlug != brandSlug)
                metadata.BrandSlug = brandSlug;

            // Instruksi supplier disimpan sekali di level brand. Seluruh SKU satu
            // game memakai desc yang sama, jadi SKU terakhir yang menang — tidak
            // ada salinan per GameProduct.
            var supplierNote = item.Desc;

            if (!string.IsNullOrWhiteSpace(supplierNote) && metadata.SupplierNote != supplierNote)
                metadata.SupplierNote = supplierNote;

            if (productsBySku.TryGetValue(sku, out var existingProduct))
            {
                var sellingPrice = Pricing.CalculateSellingPrice(
                    modalPrice, existingProduct.CustomMargin);

                var hasChanges =
                    existingProduct.Price != sellingPrice ||
                    existingProduct.Name != name ||
                    existingProduct.Brand != brand ||
                    existingProduct.Category != category ||
                    existingProduct.IsActive != isSellable;

                if (hasChanges)
                {
                    existingProduct.Price = sellingPrice;
                    existingProduct.Name = name;
                    existingProduct.Brand = brand;
                    existingProduct.Category = category;
                    existingProduct.GameCode = brand;
                    existingProduct.IsActive = isSellable;

                    updated++;
                }
            }
            else
            {
                var newProduct = new GameProduct
                {
                    BuyerSkuCode = sku,
                    Name = name,
                    Price = Pricing.CalculateSellingPrice(modalPrice, null),
                    Brand = brand,
                    Category = category,
                    GameCode = brand,
                    IsActive = isSellable
                };

                _db.GameProducts.Add(newProduct);

                // Tandai agar SKU duplikat dalam price-list tidak menambah produk ganda.
                productsBySku[sku] = newProduct;

                added++;
            }
        }

        if (activeDigiflazzSkus.Count == 0)
        {
            throw new InvalidOperationException(
                "Sync dibatalkan: Digiflazz tidak mengembalikan produk kategori Games. " +
                "Produk lama tidak dinonaktifkan untuk mencegah kehilangan katalog."
            );
        }

        var productsToDeactivate = await _db.GameProducts
            .Where(p =>
                p.Category == AppConstants.Categories.Games &&
                p.IsActive &&
                !string.IsNullOrEmpty(p.BuyerSkuCode) &&
                !activeDigiflazzSkus.Contains(p.BuyerSkuCode))
            .ToListAsync();

        foreach (var product in productsToDeactivate)
        {
            product.IsActive = false;
            deactivated++;
        }

        // Unique index BuyerSkuCode adalah proteksi terakhir bila dua sync
        // (worker + tombol admin) menulis bersamaan. SaveChanges satu transaksi,
        // jadi bentrokan berarti rollback penuh — tidak ada baris setengah jadi.
        // Dilempar sebagai InvalidOperationException agar endpoint memetakannya
        // ke 400 dan worker cukup mencatat log lalu menunggu tick berikutnya.
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Sync dibatalkan: katalog sedang ditulis proses sync lain " +
                "(BuyerSkuCode duplikat ditolak database). Coba lagi setelah sync berjalan selesai.",
                ex);
        }

        // DB sudah sukses — sync mengubah metadata (BrandSlug/SupplierNote/baru)
        // dan katalog aktif, jadi seluruh cache turunannya dibuang (best-effort):
        // metadata, popular games, serta hitungan kategori storefront & admin.
        await TryInvalidateCacheAsync(
            RedisCacheKeys.GameMetadata,
            RedisCacheKeys.PopularGames,
            RedisCacheKeys.GameCategories,
            RedisCacheKeys.GameCategoryRegistry);

        return (added, updated, deactivated);
    }

    public async Task<IEnumerable<ProductResponse>> GetAllProductsAsync()
    {
        return await _db.GameProducts
            .Select(p => new ProductResponse(p.Id, p.Name, p.Price, p.GameCode))
            .ToListAsync();
    }
    
    // HOMEPAGE: Menampilkan Brand beserta Instruksi Form UI-nya.
    // Dipaginasi per kategori (category/offset/limit): homepage merender satu
    // section per kategori, jadi tiap section cukup mengambil satu batch kecil
    // dulu — bukan seluruh katalog beserta ikonnya dalam satu response.
    public async Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync(
        string? category = null,
        int offset = 0,
        int? limit = null)
    {
        List<CategoryInfo> brands;

        var cached = await TryGetCacheAsync<List<GameMetadataResponse>>(RedisCacheKeys.GameMetadata);
        if (cached is not null)
        {
            // Cache hit: metadata dari Redis, hanya daftar brand aktif yang
            // tetap di-query dari DB (ketergantungan GameProducts, bukan scope cache).
            var activeBrands = await _db.GameProducts
                .AsNoTracking()
                .Where(product =>
                    product.Category == AppConstants.Categories.Games &&
                    product.IsActive)
                .Select(product => product.Brand)
                .Distinct()
                .ToListAsync();
            var activeSet = activeBrands.ToHashSet(StringComparer.Ordinal);

            brands = cached
                .Where(metadata => activeSet.Contains(metadata.Brand))
                .OrderBy(metadata => metadata.Brand, StringComparer.OrdinalIgnoreCase)
                .Select(metadata => new CategoryInfo(
                    metadata.Brand,
                    metadata.BrandSlug,
                    metadata.ImageUrl,
                    AppConstants.Categories.Games,
                    metadata.Categories))
                .ToList();
        }
        else
        {
            // Kategori disimpan CSV, jadi kolomnya diambil mentah lalu dipecah di
            // memori (SQL tidak bisa memecah CSV secara portabel).
            var games = await _db.GameMetadata
                .AsNoTracking()
                .Where(metadata =>
                    _db.GameProducts.Any(product =>
                        product.Brand == metadata.Brand &&
                        product.Category == AppConstants.Categories.Games &&
                        product.IsActive))
                .OrderBy(metadata => metadata.Brand)
                .Select(metadata => new
                {
                    metadata.Brand,
                    metadata.BrandSlug,
                    metadata.ImageUrl,
                    metadata.Categories
                })
                .ToListAsync();

            brands = games.Select(metadata => new CategoryInfo(
                metadata.Brand,
                metadata.BrandSlug,
                metadata.ImageUrl,
                AppConstants.Categories.Games,
                GameMetadata.ParseCategories(metadata.Categories)
            )).ToList();
        }

        return PageBrands(
            brands,
            category,
            offset,
            limit);
    }

    /// <summary>
    /// Filter kategori + potong satu halaman. Dipisah sebagai fungsi murni supaya
    /// aturan bucket virtual ("all" = tanpa filter, "lainnya" = brand tanpa
    /// kategori) bisa diuji tanpa DB.
    ///
    /// ponytail: filter di memori seperti CountCategoryUsageAsync karena kategori
    /// masih satu kolom CSV. Pindahkan ke tabel join + WHERE/LIMIT di SQL kalau
    /// katalognya sudah puluhan ribu brand.
    /// </summary>
    internal static List<CategoryInfo> PageBrands(
        IEnumerable<CategoryInfo> brands,
        string? category,
        int offset,
        int? limit)
    {
        var slug = NormalizeCategory(category);

        if (slug != null && slug != AppConstants.StorefrontCategories.AllSlug)
        {
            brands = slug == AppConstants.StorefrontCategories.UncategorizedSlug
                ? brands.Where(brand => brand.CategorySlugs.Count == 0)
                : brands.Where(brand =>
                    brand.CategorySlugs.Contains(slug, StringComparer.OrdinalIgnoreCase));
        }

        if (offset > 0)
            brands = brands.Skip(offset);

        if (limit.HasValue)
            brands = brands.Take(limit.Value);

        return brands.ToList();
    }

    // HOMEPAGE: Section "Populer" — urutan pilihan admin dari AppSettings.
    public async Task<IEnumerable<CategoryInfo>> GetPopularGameBrandsAsync()
    {
        var cached = await TryGetCacheAsync<List<CategoryInfo>>(RedisCacheKeys.PopularGames);
        if (cached is not null)
            return cached;

        var slugs = ParsePopularGameSlugs(
            await _appSettings.GetAsync(AppConstants.SystemSettings.PopularGames));

        if (slugs.Count == 0)
            return [];

        var available = await _db.GameMetadata
            .AsNoTracking()
            .Where(metadata =>
                _db.GameProducts.Any(product =>
                    product.Brand == metadata.Brand &&
                    product.Category == AppConstants.Categories.Games &&
                    product.IsActive))
            .Select(metadata => new
            {
                metadata.Brand,
                metadata.BrandSlug,
                metadata.ImageUrl,
                metadata.Categories
            })
            .ToListAsync();

        var bySlug = new Dictionary<string, CategoryInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var metadata in available)
            bySlug[metadata.BrandSlug] = new CategoryInfo(
                metadata.Brand,
                metadata.BrandSlug,
                metadata.ImageUrl,
                AppConstants.Categories.Games,
                GameMetadata.ParseCategories(metadata.Categories));

        var result = slugs
            .Where(bySlug.ContainsKey)
            .Select(slug => bySlug[slug])
            .Take(PopularBrandLimit)
            .ToList();

        await TrySetCacheAsync(RedisCacheKeys.PopularGames, result, 5);
        return result;
    }

    internal static List<string> ParsePopularGameSlugs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?
                .Where(slug => !string.IsNullOrWhiteSpace(slug))
                .Select(slug => slug.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(PopularBrandLimit)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<IEnumerable<GameMetadataResponse>> GetPopularGameOptionsAsync()
    {
        var activeBrands = await _db.GameProducts
            .AsNoTracking()
            .Where(product => product.Category == AppConstants.Categories.Games && product.IsActive)
            .Select(product => product.Brand)
            .Distinct()
            .ToListAsync();

        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .Where(metadata => activeBrands.Contains(metadata.Brand))
            .OrderBy(metadata => metadata.Brand)
            .ToListAsync();

        return metadata.Select(MapMetadataResponse).ToList();
    }

    public async Task<IEnumerable<CategoryInfo>> UpdatePopularGameBrandsAsync(
        IReadOnlyList<string> slugs)
    {
        var normalized = slugs
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => slug.Trim().ToLowerInvariant())
            .ToList();

        if (normalized.Count > PopularBrandLimit)
            throw new InvalidOperationException($"Maksimal {PopularBrandLimit} game populer.");

        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Count)
            throw new InvalidOperationException("Game populer tidak boleh duplikat.");

        var options = await GetPopularGameOptionsAsync();
        var validSlugs = options
            .Select(option => option.BrandSlug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalized.Any(slug => !validSlugs.Contains(slug)))
            throw new InvalidOperationException("Ada game yang tidak ditemukan atau tidak aktif.");

        await _appSettings.SetAsync(
            AppConstants.SystemSettings.PopularGames,
            JsonSerializer.Serialize(normalized));

        // DB sudah sukses — kegagalan cache invalidation tidak menggagalkan update;
        // key akan expired sendiri dan ter-rebuild pada read berikutnya.
        await TryInvalidateCacheAsync(RedisCacheKeys.PopularGames);

        return await GetPopularGameBrandsAsync();
    }

    // HOMEPAGE: Kategori storefront beserta jumlah brand aktif di dalamnya.
    // Urutan & nama dari registry (AppSettings), diatur admin.
    // Kategori tanpa brand aktif tidak dikembalikan (FE juga menyembunyikan yang kosong).
    //
    // Termasuk dua bucket virtual yang dipakai tab FE: "all" (seluruh brand) dan
    // "lainnya" (brand yang belum dikategorikan). Jumlah keduanya hanya bisa
    // dihitung di sini, dan FE memerlukannya sebagai total tiap section — brand-nya
    // sendiri diambil per section secara dipaginasi, jadi FE tidak pernah lagi
    // memegang seluruh katalog untuk menghitung sendiri.
    public async Task<IEnumerable<GameCategoryInfo>> GetGameCategoriesAsync()
    {
        var cached = await TryGetCacheAsync<List<GameCategoryInfo>>(RedisCacheKeys.GameCategories);
        if (cached is not null)
            return cached;

        var registry = GameCategoryRegistry.Parse(
            await _appSettings.GetAsync(AppConstants.SystemSettings.GameCategories));

        var merged = GameCategoryRegistry.Merge(
            registry,
            await GetActiveBrandCountsAsync(),
            includeEmpty: false);

        var (total, uncategorized) = await CountActiveBrandsAsync();

        // Katalog kosong: jangan kirim tab "Semua 0" yang tidak bisa diklik ke mana pun.
        if (total == 0)
            return [];

        var result = new List<GameCategoryInfo>(merged.Count + 2)
        {
            new(AppConstants.StorefrontCategories.AllSlug,
                AppConstants.StorefrontCategories.AllName,
                total)
        };

        result.AddRange(merged);

        // Tanpa entri ini brand tanpa kategori tidak punya tab maupun section.
        if (uncategorized > 0)
            result.Add(new GameCategoryInfo(
                AppConstants.StorefrontCategories.UncategorizedSlug,
                AppConstants.StorefrontCategories.UncategorizedName,
                uncategorized));

        await TrySetCacheAsync(RedisCacheKeys.GameCategories, result, 5);
        return result;
    }

    // ADMIN: registry lengkap termasuk kategori yang belum punya game,
    // supaya kategori baru bisa dipilih di editor metadata.
    public async Task<IEnumerable<GameCategoryInfo>> GetGameCategoryRegistryAsync()
    {
        var cached = await TryGetCacheAsync<List<GameCategoryInfo>>(RedisCacheKeys.GameCategoryRegistry);
        if (cached is not null)
            return cached;

        var registry = GameCategoryRegistry.Parse(
            await _appSettings.GetAsync(AppConstants.SystemSettings.GameCategories));

        var result = GameCategoryRegistry.Merge(
            registry,
            await GetActiveBrandCountsAsync(),
            includeEmpty: true)
            .ToList();

        await TrySetCacheAsync(RedisCacheKeys.GameCategoryRegistry, result, 5);
        return result;
    }

    // ADMIN: ganti seluruh registry (urutan / rename / tambah / hapus sekali simpan).
    // ponytail: replace-all, jadi last write wins. Cukup untuk satu-dua admin;
    // pakai versioning/ETag kalau nanti banyak admin mengedit bersamaan.
    public async Task<IEnumerable<GameCategoryInfo>> UpdateGameCategoryRegistryAsync(
        UpdateGameCategoriesRequest request)
    {
        var entries = GameCategoryRegistry.Validate(request.Categories);

        // Kategori yang dihapus tapi masih dipakai game harus ditolak: kalau lolos,
        // game-nya tampil di section berjudul slug mentah dan tidak bisa dipindah
        // dari editor metadata (opsinya sudah hilang dari daftar).
        var keptSlugs = entries
            .Select(entry => entry.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usage = await CountCategoryUsageAsync(onlyWithActiveProduct: false);

        var orphan = usage
            .Where(pair => !keptSlugs.Contains(pair.Key))
            .OrderBy(pair => pair.Key)
            .Select(pair => (Slug: pair.Key, Count: pair.Value))
            .FirstOrDefault();

        if (orphan.Slug != null)
            throw new InvalidOperationException(
                $"Kategori \"{orphan.Slug}\" masih dipakai {orphan.Count} game. " +
                "Pindahkan game-nya dulu sebelum menghapus kategori.");

        await _appSettings.SetAsync(
            AppConstants.SystemSettings.GameCategories,
            GameCategoryRegistry.Serialize(entries));

        // DB sudah sukses — registry berubah, storefront & admin registry sama-sama stale.
        await TryInvalidateCacheAsync(RedisCacheKeys.GameCategories, RedisCacheKeys.GameCategoryRegistry);

        return GameCategoryRegistry.Merge(
            entries,
            await GetActiveBrandCountsAsync(),
            includeEmpty: true);
    }

    /// <summary>
    /// Jumlah brand per slug kategori, hanya brand yang punya minimal satu produk
    /// Games aktif — dasar brandCount di storefront maupun admin.
    /// Game multi-kategori dihitung di setiap kategorinya, jadi total brandCount
    /// bisa lebih besar dari jumlah brand.
    /// </summary>
    private Task<Dictionary<string, int>> GetActiveBrandCountsAsync() =>
        CountCategoryUsageAsync(onlyWithActiveProduct: true);

    /// <summary>
    /// (jumlah brand aktif, jumlah brand tanpa kategori) — dasar bucket virtual
    /// "all" dan "lainnya" di GET /games/categories. Beda dari brandCount per
    /// kategori: di sini brand multi-kategori tetap dihitung sekali.
    /// </summary>
    private async Task<(int Total, int Uncategorized)> CountActiveBrandsAsync()
    {
        var categories = await _db.GameMetadata
            .AsNoTracking()
            .Where(metadata =>
                _db.GameProducts.Any(product =>
                    product.Brand == metadata.Brand &&
                    product.Category == AppConstants.Categories.Games &&
                    product.IsActive))
            .Select(metadata => metadata.Categories)
            .ToListAsync();

        return (
            categories.Count,
            categories.Count(csv => GameMetadata.ParseCategories(csv).Count == 0));
    }

    // ponytail: pemecahan CSV + hitung dilakukan di memori karena kategori
    // disimpan satu kolom. Katalognya ratusan baris; kalau nanti puluhan ribu,
    // pindahkan kategori ke tabel join dan hitung dengan GROUP BY di SQL.
    private async Task<Dictionary<string, int>> CountCategoryUsageAsync(
        bool onlyWithActiveProduct)
    {
        var query = _db.GameMetadata
            .AsNoTracking()
            .Where(metadata => metadata.Categories != null);

        if (onlyWithActiveProduct)
            query = query.Where(metadata =>
                _db.GameProducts.Any(product =>
                    product.Brand == metadata.Brand &&
                    product.Category == AppConstants.Categories.Games &&
                    product.IsActive));

        var rows = await query
            .Select(metadata => metadata.Categories!)
            .ToListAsync();

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var csv in rows)
            foreach (var slug in GameMetadata.ParseCategories(csv))
                counts[slug] = counts.GetValueOrDefault(slug) + 1;

        return counts;
    }
    
    public async Task<GameDetailResponse?> GetGameDetailAsync(string brandSlug)
    {
        // Route value dinormalisasi dengan aturan slug yang sama seperti saat
        // disimpan, lalu dicocokkan langsung ke kolom — tidak ada sanitasi
        // brand di SQL lagi.
        var slug = Slug.From(brandSlug);

        if (slug == null)
            return null;

        // Metadata dari cache global; query produk tetap ke DB (bukan scope cache).
        var metadataList = await TryGetGameMetadataListAsync();
        var metadata = metadataList.FirstOrDefault(m => m.BrandSlug == slug);

        if (metadata == null)
            return null;

        var products = await _db.GameProducts
            .AsNoTracking()
            .Where(p =>
                p.Brand == metadata.Brand &&
                p.Category == AppConstants.Categories.Games &&
                p.IsActive &&
                p.Id != metadata.UsernameCheckProductId)
            .OrderBy(p => p.Price)
            .Select(p => new GameProductItemResponse(
                p.Id,
                p.Name,
                p.Price,
                p.GameCode,
                p.Brand,
                p.Category,
                p.ImageUrl,
                p.CustomMargin
            ))
            .ToListAsync();

        return new GameDetailResponse(
            metadata.Brand,
            metadata.BrandSlug,
            metadata.ImageUrl,
            metadata.InputFields,
            metadata.UsernameCheckProductId.HasValue,
            products
        );
    }

    public async Task<UsernameCheckResponse> CheckUsernameAsync(
        string brandSlug,
        Dictionary<string, string?> targetValues,
        CancellationToken cancellationToken = default)
    {
        var slug = Slug.From(brandSlug)
            ?? throw new InvalidOperationException("Game tidak ditemukan.");
        var metadata = await _db.GameMetadata.AsNoTracking()
            .FirstOrDefaultAsync(item => item.BrandSlug == slug, cancellationToken)
            ?? throw new InvalidOperationException("Game tidak ditemukan.");

        if (!metadata.UsernameCheckProductId.HasValue)
            throw new InvalidOperationException("Cek nama akun belum tersedia untuk game ini.");

        var product = await _db.GameProducts.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == metadata.UsernameCheckProductId &&
            item.Brand == metadata.Brand &&
            item.Category == AppConstants.Categories.Games &&
            item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Produk cek nama akun tidak tersedia.");

        var fields = GameInputSchemaSerializer.Deserialize(metadata.InputSchemaJson);
        OrderTargetRenderer.ValidateTargetValues(fields, targetValues);
        var customerNo = OrderTargetRenderer.RenderTargetTemplate(
            metadata.TargetTemplate, fields, targetValues);
        var result = await _digiflazzProvider.CheckUsernameAsync(
            product.BuyerSkuCode,
            customerNo,
            $"CHECK-{Guid.NewGuid():N}",
            cancellationToken);

        return new UsernameCheckResponse(result.IsValid, result.Username, result.Message);
    }

    public async Task<PagedResult<GameProduct>> GetPagedProductsAsync(
        string? search,
        string? brand,
        int page,
        int limit)
    {
        var query = _db.GameProducts
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(brand))
        {
            var normalizedBrand = brand.Trim();

            query = query.Where(product =>
                product.Brand == normalizedBrand);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();

            query = query.Where(product =>
                product.Name.Contains(normalizedSearch) ||
                product.Brand.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(product => product.Price)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)limit);

        return new PagedResult<GameProduct>(
            items,
            totalCount,
            totalPages,
            page);
    }

    public async Task<GameProduct> CreateProductAsync(GameProduct product)
    {
        // SKU wajib: produk tanpa BuyerSkuCode tidak bisa diorder (Digiflazz
        // butuh SKU-nya), dan kolomnya kini unique — beberapa produk manual
        // dengan SKU kosong akan ditolak database.
        if (string.IsNullOrWhiteSpace(product.BuyerSkuCode))
            throw new InvalidOperationException("BuyerSkuCode wajib diisi.");

        product.BuyerSkuCode = product.BuyerSkuCode.Trim();

        _db.GameProducts.Add(product);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                $"BuyerSkuCode '{product.BuyerSkuCode}' sudah dipakai produk lain.", ex);
        }

        return product;
    }

    public async Task<string?> UpdateProductIconAsync(int productId, ImageFileData file, CancellationToken cancellationToken = default)
    {
        var product = await _db.GameProducts.FindAsync(productId);
        if (product == null) return null;

        await ImageFileValidator.EnsureValidImageAsync(file, cancellationToken);

        if (!string.IsNullOrEmpty(product.ImageUrl))
        {
            await _r2Provider.DeleteFileAsync(product.ImageUrl, cancellationToken);
        }

        var newImageUrl = await _r2Provider.UploadFileAsync(file, "icon-item", cancellationToken);

        product.ImageUrl = newImageUrl;
        await _db.SaveChangesAsync();

        return newImageUrl;
    }

    public async Task<int> CleanDummyProductsAsync()
    {
        var dummyProducts = await _db.GameProducts
            .Where(p => string.IsNullOrEmpty(p.BuyerSkuCode))
            .ToListAsync();

        if (dummyProducts.Count == 0)
            return 0;

        int count = dummyProducts.Count;
        _db.GameProducts.RemoveRange(dummyProducts);
        await _db.SaveChangesAsync();

        return count;
    }

    public async Task<bool> UpdateProductMarginAsync(int productId, decimal? margin)
    {
        var product = await _db.GameProducts.FindAsync(productId);
        if (product == null) return false;

        // Harga modal tidak disimpan — dibalik dari harga jual dengan margin lama.
        decimal modalPrice =
            product.Price - Pricing.MarginOf(product.CustomMargin);

        product.Price = Pricing.CalculateSellingPrice(modalPrice, margin);
        product.CustomMargin = margin;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<int> GetMidtransExpiryAsync()
    {
        var value = await _appSettings.GetAsync(AppConstants.SystemSettings.MidtransExpiry);

        return int.TryParse(value, out var minutes) ? minutes : 5; // default 5 menit
    }

    public async Task SetMidtransExpiryAsync(int expiryMinutes)
    {
        await _appSettings.SetAsync(
            AppConstants.SystemSettings.MidtransExpiry,
            expiryMinutes.ToString());
    }

    // ── Redis L2 cache helpers (read-heavy storefront data) ──────────────
    // MySQL tetap SSOT: cache failure tidak boleh menggagalkan endpoint.

    /// <summary>
    /// Sumber metadata global untuk semua read path yang aman dicache
    /// (admin list/detail, detail publik, brands list). Order path TIDAK
    /// memakai ini — PlaceOrderAsync tetap query DB langsung.
    /// TTL 10 menit: safety net; invalidation utama tetap explicit DELETE
    /// pada sync/update/cover.
    /// </summary>
    private async Task<List<GameMetadataResponse>> TryGetGameMetadataListAsync()
    {
        var cached = await TryGetCacheAsync<List<GameMetadataResponse>>(RedisCacheKeys.GameMetadata);
        if (cached is not null)
            return cached;

        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .OrderBy(m => m.Brand)
            .ToListAsync();

        var result = metadata
            .Select(MapMetadataResponse)
            .ToList();

        await TrySetCacheAsync(RedisCacheKeys.GameMetadata, result, 10);
        return result;
    }

    private async Task<T?> TryGetCacheAsync<T>(string key) where T : class
    {
        try
        {
            var json = await _redis.GetAsync(key);
            return json is null ? null : JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET gagal untuk {CacheKey}; fallback ke database.", key);
            return null;
        }
    }

    private async Task TrySetCacheAsync<T>(string key, T value, int ttlMinutes)
    {
        try
        {
            await _redis.SetAsync(key, JsonSerializer.Serialize(value), CacheTtl(ttlMinutes));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET gagal untuk {CacheKey}; melewati cache.", key);
        }
    }

    private async Task TryInvalidateCacheAsync(params string[] keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await _redis.DeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis DELETE gagal untuk {CacheKey}; cache akan expired sendiri.", key);
            }
        }
    }

    public async Task<string?> UpdateGameCoverAsync(int metadataId, ImageFileData file, CancellationToken cancellationToken = default)
    {
        await ImageFileValidator.EnsureValidImageAsync(file, cancellationToken);

        var meta = await _db.GameMetadata.FindAsync(metadataId);
        if (meta == null) return null;

        // hapus gambar lama
        if (!string.IsNullOrEmpty(meta.ImageUrl))
        {
            await _r2Provider.DeleteFileAsync(meta.ImageUrl, cancellationToken);
        }

        var imageUrl = await _r2Provider.UploadFileAsync(file, "icon-cover", cancellationToken);

        meta.ImageUrl = imageUrl;
        await _db.SaveChangesAsync(cancellationToken);

        // DB sudah sukses — cover baru harus tampil pada read berikutnya (best-effort).
        await TryInvalidateCacheAsync(RedisCacheKeys.GameMetadata);

        return imageUrl;
    }

    private static GameMetadataResponse MapMetadataResponse(
        GameMetadata metadata)
    {
        return new GameMetadataResponse(
            metadata.Id,
            metadata.Brand,
            metadata.BrandSlug,
            metadata.ImageUrl,
            GameInputSchemaSerializer.Deserialize(metadata.InputSchemaJson),
            metadata.TargetTemplate,
            metadata.SupplierNote,
            GameMetadata.ParseCategories(metadata.Categories),
            metadata.UsernameCheckProductId
        );
    }

    private async Task<int?> ValidateUsernameCheckProductAsync(string brand, int? productId)
    {
        if (!productId.HasValue)
            return null;

        var exists = await _db.GameProducts.AnyAsync(product =>
            product.Id == productId &&
            product.Brand == brand &&
            product.Category == AppConstants.Categories.Games &&
            product.IsActive);

        if (!exists)
            throw new InvalidOperationException("Produk cek nama harus produk game aktif dari brand ini.");

        return productId;
    }

    private static List<GameInputField> NormalizeAndValidateInputFields(
        IReadOnlyList<GameInputField>? inputFields)
    {
        if (inputFields == null || inputFields.Count == 0)
        {
            throw new InvalidOperationException(
                "Game harus memiliki minimal satu field input.");
        }

        var normalizedFields = inputFields
            .OrderBy(field => field.Order)
            .Select((field, index) => NormalizeInputField(field, index + 1))
            .ToList();

        var duplicateKey = normalizedFields
            .GroupBy(
                field => field.Key,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateKey != null)
        {
            throw new InvalidOperationException(
                $"Key input '{duplicateKey.Key}' tidak boleh duplikat.");
        }

        return normalizedFields;
    }
    
    private static GameInputField NormalizeInputField(
        GameInputField field,
        int order)
    {
        var key = NormalizeFieldKey(field.Key);

        var label = field.Label?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException(
                $"Label untuk field '{key}' wajib diisi.");
        }

        var type = field.Type?
                       .Trim()
                       .ToLowerInvariant()
                   ?? string.Empty;

        if (type != AppConstants.GameInputFieldTypes.Text &&
            type != AppConstants.GameInputFieldTypes.Number &&
            type != AppConstants.GameInputFieldTypes.Select)
        {
            throw new InvalidOperationException(
                $"Tipe input '{field.Type}' tidak didukung.");
        }

        var options = (field.Options ?? [])
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (type == AppConstants.GameInputFieldTypes.Select &&
            options.Count == 0)
        {
            throw new InvalidOperationException(
                $"Field select '{label}' harus memiliki minimal satu pilihan.");
        }

        if (type != AppConstants.GameInputFieldTypes.Select)
        {
            options = [];
        }

        return new GameInputField
        {
            Key = key,
            Label = label,
            Placeholder = field.Placeholder?.Trim() ?? string.Empty,
            Type = type,
            Required = field.Required,
            Order = order,
            Options = options
        };
    }
    
    private static string NormalizeFieldKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Key input wajib diisi.");
        }

        var trimmedKey = key.Trim();

        if (!char.IsLetter(trimmedKey[0]))
        {
            throw new InvalidOperationException(
                $"Key '{trimmedKey}' harus diawali huruf.");
        }

        if (!trimmedKey.All(character =>
                char.IsLetterOrDigit(character) ||
                character == '_'))
        {
            throw new InvalidOperationException(
                $"Key '{trimmedKey}' hanya boleh berisi huruf, angka, dan underscore.");
        }

        return char.ToLowerInvariant(trimmedKey[0]) +
               trimmedKey[1..];
    }
    
    internal static string ValidateTargetTemplate(
        string targetTemplate,
        IReadOnlyList<GameInputField> inputFields)
    {
        if (string.IsNullOrWhiteSpace(targetTemplate))
        {
            throw new InvalidOperationException(
                "Target template wajib diisi.");
        }

        var normalizedTemplate = targetTemplate.Trim();

        var templateKeys = ExtractTemplateKeys(normalizedTemplate);

        if (templateKeys.Count == 0)
        {
            throw new InvalidOperationException(
                "Target template harus menggunakan minimal satu input field.");
        }

        var availableKeys = inputFields
            .Select(field => field.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Arah 1: setiap placeholder di template harus ada di schema.
        var unknownKey = templateKeys
            .FirstOrDefault(key => !availableKeys.Contains(key));

        if (unknownKey != null)
        {
            throw new InvalidOperationException(
                $"Placeholder '{unknownKey}' pada target template tidak dikenali.");
        }

        // Arah 2: setiap key di schema wajib direferensikan template — cegah drift
        // yang membuat field (mis. zoneId) diabaikan diam-diam saat render customer_no.
        var missingKey = inputFields
            .Select(field => field.Key)
            .FirstOrDefault(key => !templateKeys.Contains(key));

        if (missingKey != null)
        {
            throw new InvalidOperationException(
                $"Target template belum mereferensikan input key '{missingKey}' yang wajib.");
        }

        return normalizedTemplate;
    }
    
    private static HashSet<string> ExtractTemplateKeys(
        string targetTemplate)
    {
        var keys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var startIndex = -1;

        for (var index = 0; index < targetTemplate.Length; index++)
        {
            if (targetTemplate[index] == '{')
            {
                startIndex = index + 1;
                continue;
            }

            if (targetTemplate[index] != '}' || startIndex < 0)
                continue;

            var keyLength = index - startIndex;

            if (keyLength > 0)
            {
                var key = targetTemplate.Substring(
                    startIndex,
                    keyLength);

                keys.Add(key);
            }

            startIndex = -1;
        }

        return keys;
    }
    
}


