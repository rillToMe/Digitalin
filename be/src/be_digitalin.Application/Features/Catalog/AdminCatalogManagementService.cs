using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Application.Serialization;
using be_digitalin.Application.Validation;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Products;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Settings;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Games;

/// <summary>
/// Use case pengelolaan katalog admin: CRUD produk, margin harga, pembersihan dummy,
/// metadata brand, skema input dinamis, cover/icon R2, kategori storefront, dan game populer.
/// </summary>
public sealed class AdminCatalogManagementService
{
    private readonly IApplicationDbContext _db;
    private readonly ICloudflareR2Provider _r2Provider;
    private readonly IAppSettingsService _appSettings;
    private readonly CatalogCacheHelper _cache;
    private readonly StorefrontCatalogService _storefront;

    public AdminCatalogManagementService(
        IApplicationDbContext db,
        ICloudflareR2Provider r2Provider,
        IAppSettingsService appSettings,
        CatalogCacheHelper cache,
        StorefrontCatalogService storefront)
    {
        _db = db;
        _r2Provider = r2Provider;
        _appSettings = appSettings;
        _cache = cache;
        _storefront = storefront;
    }

    public async Task<IReadOnlyList<GameMetadataResponse>> GetGameMetadataAsync() =>
        await _cache.TryGetGameMetadataListAsync();

    public async Task<GameMetadataResponse?> GetGameMetadataByIdAsync(int id)
    {
        var list = await _cache.TryGetGameMetadataListAsync();
        return list.FirstOrDefault(m => m.Id == id);
    }

    public async Task<GameMetadataResponse?> UpdateGameMetadataAsync(
        int id,
        UpdateGameMetadataRequest request)
    {
        var metadata = await _db.GameMetadata
            .FirstOrDefaultAsync(m => m.Id == id);

        if (metadata == null)
            return null;

        var normalizedFields = InputSchemaRules.NormalizeAndValidateInputFields(
            request.InputFields);

        var normalizedTargetTemplate = InputSchemaRules.ValidateTargetTemplate(
            request.TargetTemplate,
            normalizedFields);

        metadata.InputSchemaJson = GameInputSchemaSerializer.Serialize(normalizedFields);
        metadata.TargetTemplate = normalizedTargetTemplate;
        metadata.Categories = ProductService.NormalizeCategories(request.Categories);
        metadata.UsernameCheckProductId = await ValidateUsernameCheckProductAsync(
            metadata.Brand,
            request.UsernameCheckProductId);

        await _db.SaveChangesAsync();
        await _cache.TryInvalidateCacheAsync(RedisCacheKeys.GameMetadata);

        return CatalogCacheHelper.MapMetadataResponse(metadata);
    }

    public async Task<string?> UpdateGameCoverAsync(
        int metadataId,
        ImageFileData file,
        CancellationToken cancellationToken = default)
    {
        await ImageFileValidator.EnsureValidImageAsync(file, cancellationToken);

        var meta = await _db.GameMetadata.FindAsync(metadataId);
        if (meta == null) return null;

        if (!string.IsNullOrEmpty(meta.ImageUrl))
        {
            await _r2Provider.DeleteFileAsync(meta.ImageUrl, cancellationToken);
        }

        var imageUrl = await _r2Provider.UploadFileAsync(file, "icon-cover", cancellationToken);

        meta.ImageUrl = imageUrl;
        await _db.SaveChangesAsync(cancellationToken);

        await _cache.TryInvalidateCacheAsync(RedisCacheKeys.GameMetadata);
        return imageUrl;
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

        return metadata.Select(CatalogCacheHelper.MapMetadataResponse).ToList();
    }

    public async Task<IEnumerable<CategoryInfo>> UpdatePopularGameBrandsAsync(
        IReadOnlyList<string> slugs)
    {
        var normalized = slugs
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => slug.Trim().ToLowerInvariant())
            .ToList();

        if (normalized.Count > StorefrontCatalogService.PopularBrandLimit)
            throw new InvalidOperationException($"Maksimal {StorefrontCatalogService.PopularBrandLimit} game populer.");

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

        await _cache.TryInvalidateCacheAsync(RedisCacheKeys.PopularGames);
        return await _storefront.GetPopularGameBrandsAsync();
    }

    public async Task<IEnumerable<GameCategoryInfo>> GetGameCategoryRegistryAsync()
    {
        var cached = await _cache.TryGetCacheAsync<List<GameCategoryInfo>>(RedisCacheKeys.GameCategoryRegistry);
        if (cached is not null)
            return cached;

        var registry = GameCategoryRegistry.Parse(
            await _appSettings.GetAsync(AppConstants.SystemSettings.GameCategories));

        var result = GameCategoryRegistry.Merge(
            registry,
            await _storefront.CountCategoryUsageAsync(onlyWithActiveProduct: true),
            includeEmpty: true)
            .ToList();

        await _cache.TrySetCacheAsync(RedisCacheKeys.GameCategoryRegistry, result, 5);
        return result;
    }

    public async Task<IEnumerable<GameCategoryInfo>> UpdateGameCategoryRegistryAsync(
        UpdateGameCategoriesRequest request)
    {
        var entries = GameCategoryRegistry.Validate(request.Categories);

        var keptSlugs = entries
            .Select(entry => entry.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usage = await _storefront.CountCategoryUsageAsync(onlyWithActiveProduct: false);

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

        await _cache.TryInvalidateCacheAsync(RedisCacheKeys.GameCategories, RedisCacheKeys.GameCategoryRegistry);

        return GameCategoryRegistry.Merge(
            entries,
            await _storefront.CountCategoryUsageAsync(onlyWithActiveProduct: true),
            includeEmpty: true);
    }

    public async Task<IEnumerable<ProductResponse>> GetAllProductsAsync() =>
        await _db.GameProducts
            .Select(p => new ProductResponse(p.Id, p.Name, p.Price, p.GameCode))
            .ToListAsync();

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
            query = query.Where(product => product.Brand == normalizedBrand);
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

        var totalPages = (int)Math.Ceiling(totalCount / (double)limit);

        return new PagedResult<GameProduct>(
            items,
            totalCount,
            totalPages,
            page);
    }

    public async Task<GameProduct> CreateProductAsync(GameProduct product)
    {
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

    public async Task<string?> UpdateProductIconAsync(
        int productId,
        ImageFileData file,
        CancellationToken cancellationToken = default)
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
        await _db.SaveChangesAsync(cancellationToken);

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

        decimal modalPrice = product.Price - Pricing.MarginOf(product.CustomMargin);
        product.Price = Pricing.CalculateSellingPrice(modalPrice, margin);
        product.CustomMargin = margin;
        await _db.SaveChangesAsync();

        return true;
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
}
