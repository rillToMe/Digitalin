using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Constants;
using be_digitalin.Providers;
using be_digitalin.Services.GameTemplates;
using be_digitalin.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace be_digitalin.Services;

public class ProductService : IProductService
{
    private readonly DigitalinDbContext _db;
    private readonly CloudflareR2Provider _r2Provider;
    private readonly DigiflazzProvider _digiflazzProvider;

    /// <summary>Jendela penjualan untuk section "Populer" (hari).</summary>
    private const int PopularWindowDays = 30;

    /// <summary>
    /// Jumlah game di section "Populer". Sama dengan batas grid homepage,
    /// jadi section-nya penuh tanpa tombol "Tampilkan lebih banyak".
    /// </summary>
    private const int PopularBrandLimit = 12;

    /// <summary>Apa pun yang bukan huruf/angka jadi pemisah slug kategori.</summary>
    private static readonly Regex NonSlugCharacters =
        new("[^a-z0-9]+", RegexOptions.Compiled);

    public ProductService(
        DigitalinDbContext db, 
        CloudflareR2Provider r2Provider, 
        DigiflazzProvider digiflazzProvider)
    {
        _db = db;
        _r2Provider = r2Provider;
        _digiflazzProvider = digiflazzProvider;
    }
    
    public async Task<IReadOnlyList<GameMetadataResponse>>
        GetGameMetadataAsync()
    {
        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .OrderBy(m => m.Brand)
            .ToListAsync();

        return metadata
            .Select(MapMetadataResponse)
            .ToList();
    }

    public async Task<GameMetadataResponse?>
        GetGameMetadataByIdAsync(int id)
    {
        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        return metadata == null
            ? null
            : MapMetadataResponse(metadata);
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

        await _db.SaveChangesAsync();

        return MapMetadataResponse(metadata);
    }

    /// <summary>
    /// Normalisasi kategori storefront dari input admin menjadi slug kebab-case:
    /// lowercase, karakter non-alfanumerik → hyphen ("E-Money" → "e-money",
    /// "Konsol, Game" → "konsol-game"). Input kosong/whitespace → null.
    /// Hanya menyisakan [a-z0-9-] supaya slug aman dipakai di URL/anchor FE
    /// dan sebagai elemen CSV di GameMetadata.Categories.
    /// </summary>
    internal static string? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;

        var slug = NonSlugCharacters
            .Replace(category.ToLowerInvariant(), "-")
            .Trim('-');

        return slug.Length == 0 ? null : slug;
    }

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

    /// <summary>
    /// Slug brand seperti yang dipakai URL detail game — sama dengan ekspresi
    /// yang diterjemahkan ke SQL di GetGameDetailAsync, hanya versi in-memory.
    /// </summary>
    private static string BrandSlug(string brand) =>
        brand.ToLower().Replace(" ", "-");
    
    // logic sync
    public async Task<(int Added, int Updated, int Deactivated)> SyncCatalogFromDigiflazzAsync()
    {
        var dataArray = await _digiflazzProvider.GetPriceListAsync();

        int added = 0;
        int updated = 0;
        int deactivated = 0;

        var activeDigiflazzSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Load katalog sekali ke memori (hindari N+1 di dalam loop) ──
        // Brand di-lookup case-insensitive agar sepadan dengan kolasi default MySQL.
        var existingBrands = await _db.GameMetadata
            .Select(m => m.Brand)
            .ToListAsync();
        var knownBrands = new HashSet<string>(existingBrands, StringComparer.OrdinalIgnoreCase);

        // SKU unik → lookup per item cukup dari dictionary (bukan query).
        var products = await _db.GameProducts.ToListAsync();
        var productsBySku = products
            .Where(p => !string.IsNullOrEmpty(p.BuyerSkuCode))
            .GroupBy(p => p.BuyerSkuCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in dataArray)
        {
            var category = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.Category)
                .GetString();

            if (category != AppConstants.Categories.Games)
                continue;

            var sku = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.BuyerSkuCode)
                .GetString() ?? string.Empty;

            var name = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.ProductName)
                .GetString() ?? string.Empty;

            var modalPrice = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.Price)
                .GetDecimal();

            var brand = item
                .GetProperty(AppConstants.DigiflazzResponseKeys.Brand)
                .GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(sku))
                continue;

            activeDigiflazzSkus.Add(sku);

            if (!knownBrands.Contains(brand))
            {
                _db.GameMetadata.Add(new GameMetadata
                {
                    Brand = brand,
                    InputSchemaJson = GameInputSchemaSerializer.Serialize(
                        GameTemplateFactory.CreateDefaultInputFields()),
                    TargetTemplate = GameTemplateFactory.DefaultTargetTemplate
                });

                // Tandai agar SKU lain dengan brand sama tidak menambah metadata duplikat.
                knownBrands.Add(brand);
            }

            if (productsBySku.TryGetValue(sku, out var existingProduct))
            {
                var margin = existingProduct.CustomMargin ?? 0m;
                var sellingPrice = modalPrice + margin;

                var hasChanges =
                    existingProduct.Price != sellingPrice ||
                    existingProduct.Name != name ||
                    existingProduct.Brand != brand ||
                    existingProduct.Category != category ||
                    !existingProduct.IsActive;

                if (hasChanges)
                {
                    existingProduct.Price = sellingPrice;
                    existingProduct.Name = name;
                    existingProduct.Brand = brand;
                    existingProduct.Category = category;
                    existingProduct.GameCode = brand;
                    existingProduct.IsActive = true;

                    updated++;
                }
            }
            else
            {
                var newProduct = new GameProduct
                {
                    BuyerSkuCode = sku,
                    Name = name,
                    Price = modalPrice,
                    Brand = brand,
                    Category = category,
                    GameCode = brand,
                    IsActive = true
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

        await _db.SaveChangesAsync();

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
                metadata.ImageUrl,
                metadata.Categories
            })
            .ToListAsync();

        return PageBrands(
            games.Select(metadata => new CategoryInfo(
                metadata.Brand,
                BrandSlug(metadata.Brand),
                metadata.ImageUrl,
                AppConstants.Categories.Games,
                GameMetadata.ParseCategories(metadata.Categories)
            )),
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

    // HOMEPAGE: Section "Populer" — peringkat otomatis dari penjualan, bukan kurasi manual.
    // ponytail: dihitung ulang tiap request tanpa cache. Bungkus IMemoryCache
    // (TTL beberapa menit) kalau TopupOrders sudah besar — hasilnya berubah lambat.
    public async Task<IEnumerable<CategoryInfo>> GetPopularGameBrandsAsync()
    {
        var since = DateTime.UtcNow.AddDays(PopularWindowDays * -1);

        // Peringkat dari snapshot BrandName di order, bukan dari relasi produk:
        // produk boleh dihapus/diganti tanpa menghilangkan riwayat penjualannya.
        // Hanya COMPLETED yang dihitung — PENDING/EXPIRED bukan penjualan.
        var ranking = await _db.TopupOrders
            .AsNoTracking()
            .Where(order =>
                order.Status == AppConstants.OrderStatus.Completed &&
                order.OrderDate >= since)
            .GroupBy(order => order.BrandName)
            .Select(group => new { Brand = group.Key, Sales = group.Count() })
            .OrderByDescending(item => item.Sales)
            .ThenBy(item => item.Brand)
            .ToListAsync();

        if (ranking.Count == 0)
            return [];

        var rankedBrands = ranking.Select(item => item.Brand).ToList();

        // Brand yang metadata/produk aktifnya sudah tidak ada harus gugur:
        // kartunya akan mengarah ke halaman detail kosong.
        var available = await _db.GameMetadata
            .AsNoTracking()
            .Where(metadata =>
                rankedBrands.Contains(metadata.Brand) &&
                _db.GameProducts.Any(product =>
                    product.Brand == metadata.Brand &&
                    product.Category == AppConstants.Categories.Games &&
                    product.IsActive))
            .Select(metadata => new
            {
                metadata.Brand,
                metadata.ImageUrl,
                metadata.Categories
            })
            .ToListAsync();

        // Casing snapshot bisa beda dari GameMetadata.Brand; indexer (bukan
        // ToDictionary) supaya brand duplikat tidak melempar.
        var byBrand = new Dictionary<string, CategoryInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var metadata in available)
            byBrand[metadata.Brand] = new CategoryInfo(
                metadata.Brand,
                BrandSlug(metadata.Brand),
                metadata.ImageUrl,
                AppConstants.Categories.Games,
                GameMetadata.ParseCategories(metadata.Categories));

        return rankedBrands
            .Where(byBrand.ContainsKey)
            .Select(brand => byBrand[brand])
            .Take(PopularBrandLimit)
            .ToList();
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
        var registry = GameCategoryRegistry.Parse(
            await GetSettingAsync(AppConstants.SystemSettings.GameCategories));

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

        return result;
    }

    // ADMIN: registry lengkap termasuk kategori yang belum punya game,
    // supaya kategori baru bisa dipilih di editor metadata.
    public async Task<IEnumerable<GameCategoryInfo>> GetGameCategoryRegistryAsync()
    {
        var registry = GameCategoryRegistry.Parse(
            await GetSettingAsync(AppConstants.SystemSettings.GameCategories));

        return GameCategoryRegistry.Merge(
            registry,
            await GetActiveBrandCountsAsync(),
            includeEmpty: true);
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

        await SetSettingAsync(
            AppConstants.SystemSettings.GameCategories,
            GameCategoryRegistry.Serialize(entries));

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
        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.Brand.ToLower().Replace(" ", "-") == brandSlug.ToLower());

        if (metadata == null)
            return null;

        var products = await _db.GameProducts
            .AsNoTracking()
            .Where(p =>
                p.Brand == metadata.Brand &&
                p.Category == AppConstants.Categories.Games &&
                p.IsActive)
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
            metadata.Brand.ToLower().Replace(" ", "-"),
            metadata.ImageUrl,
            GameInputSchemaSerializer.Deserialize(metadata.InputSchemaJson),
            products
        );
    }
    
    
    public async Task<IEnumerable<GameProduct>> GetProductsByBrandAsync(string brand)
    {
        var dbBrandName = brand.Replace("-", " ").ToLower();
        return await _db.GameProducts
            .Where(p => p.Brand.ToLower() == dbBrandName)
            .OrderBy(p => p.Price) 
            .ToListAsync();
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
        _db.GameProducts.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<string?> UpdateProductIconAsync(int productId, IFormFile file)
    {
        var product = await _db.GameProducts.FindAsync(productId);
        if (product == null) return null;

        if (!string.IsNullOrEmpty(product.ImageUrl))
        {
            await _r2Provider.DeleteFileAsync(product.ImageUrl);
        }

        var newImageUrl = await _r2Provider.UploadFileAsync(file, "icon-item");

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

        decimal oldMargin = product.CustomMargin ?? 0m;
        decimal modalPrice = product.Price - oldMargin;

        decimal newMargin = margin ?? 0m;

        product.Price = modalPrice + newMargin;
        product.CustomMargin = margin;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<int> GetMidtransExpiryAsync()
    {
        var value = await GetSettingAsync(AppConstants.SystemSettings.MidtransExpiry);

        return int.TryParse(value, out var minutes) ? minutes : 5; // default 5 menit
    }

    public async Task SetMidtransExpiryAsync(int expiryMinutes)
    {
        await SetSettingAsync(
            AppConstants.SystemSettings.MidtransExpiry,
            expiryMinutes.ToString());
    }

    /// <summary>Baca satu baris AppSettings. Null bila key belum pernah disimpan.</summary>
    private async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);

        return setting?.Value;
    }

    /// <summary>Upsert satu baris AppSettings.</summary>
    private async Task SetSettingAsync(string key, string value)
    {
        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == key);

        if (setting == null)
        {
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string?> UpdateGameCoverAsync(int metadataId, IFormFile file)
    {
        var meta = await _db.GameMetadata.FindAsync(metadataId);
        if (meta == null) return null;

        // hapus gambar lama
        if (!string.IsNullOrEmpty(meta.ImageUrl))
        {
            await _r2Provider.DeleteFileAsync(meta.ImageUrl);
        }

        var imageUrl = await _r2Provider.UploadFileAsync(file, "icon-cover");

        meta.ImageUrl = imageUrl;
        await _db.SaveChangesAsync();

        return imageUrl;
    }

    private static GameMetadataResponse MapMetadataResponse(
        GameMetadata metadata)
    {
        return new GameMetadataResponse(
            metadata.Id,
            metadata.Brand,
            metadata.ImageUrl,
            GameInputSchemaSerializer.Deserialize(metadata.InputSchemaJson),
            metadata.TargetTemplate,
            GameMetadata.ParseCategories(metadata.Categories)
        );
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