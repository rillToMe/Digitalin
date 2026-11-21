using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Application.Serialization;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Orders;
using be_digitalin.DTOs;
using be_digitalin.Services.Settings;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Games;

/// <summary>
/// Use case publik storefront katalog: daftar brand per kategori, brand populer,
/// brand terakhir dibeli, daftar kategori aktif, detail produk brand, dan cek username akun.
/// </summary>
public sealed class StorefrontCatalogService
{
    public const int PopularBrandLimit = 12;
    public const int RecentBrandLimit = 12;

    private readonly IApplicationDbContext _db;
    private readonly IDigiflazzProvider _digiflazzProvider;
    private readonly IAppSettingsService _appSettings;
    private readonly CatalogCacheHelper _cache;

    public StorefrontCatalogService(
        IApplicationDbContext db,
        IDigiflazzProvider digiflazzProvider,
        IAppSettingsService appSettings,
        CatalogCacheHelper cache)
    {
        _db = db;
        _digiflazzProvider = digiflazzProvider;
        _appSettings = appSettings;
        _cache = cache;
    }

    public async Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync(
        string? category = null,
        int offset = 0,
        int? limit = null)
    {
        List<CategoryInfo> brands;

        var cached = await _cache.TryGetCacheAsync<List<GameMetadataResponse>>(RedisCacheKeys.GameMetadata);
        if (cached is not null)
        {
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

        return PageBrands(brands, category, offset, limit);
    }

    public static List<CategoryInfo> PageBrands(
        IEnumerable<CategoryInfo> brands,
        string? category,
        int offset,
        int? limit)
    {
        var slug = Slug.From(category);

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

    public async Task<IEnumerable<CategoryInfo>> GetPopularGameBrandsAsync()
    {
        var cached = await _cache.TryGetCacheAsync<List<CategoryInfo>>(RedisCacheKeys.PopularGames);
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

        await _cache.TrySetCacheAsync(RedisCacheKeys.PopularGames, result, 5);
        return result;
    }

    public async Task<IEnumerable<CategoryInfo>> GetRecentGameBrandsAsync(
        int? customerId,
        IReadOnlyList<string>? guestSlugs = null)
    {
        List<string>? orderedBrandNames = null;
        List<string>? orderedSlugs = null;

        if (customerId.HasValue)
        {
            var purchasedBrands = await _db.TopupOrders
                .AsNoTracking()
                .Where(order =>
                    order.CustomerId == customerId.Value &&
                    AppConstants.OrderStatus.PaidStatuses.Contains(order.Status))
                .OrderByDescending(order => order.OrderDate)
                .Select(order => order.BrandName)
                .ToListAsync();

            orderedBrandNames = purchasedBrands
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (orderedBrandNames.Count == 0)
                return [];
        }
        else
        {
            orderedSlugs = (guestSlugs ?? [])
                .Select(Slug.From)
                .Where(slug => slug != null)
                .Select(slug => slug!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (orderedSlugs.Count == 0)
                return [];
        }

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

        if (orderedBrandNames != null)
        {
            var byBrand = available.ToDictionary(
                metadata => metadata.Brand,
                metadata => new CategoryInfo(
                    metadata.Brand,
                    metadata.BrandSlug,
                    metadata.ImageUrl,
                    AppConstants.Categories.Games,
                    GameMetadata.ParseCategories(metadata.Categories)),
                StringComparer.Ordinal);

            return orderedBrandNames
                .Where(byBrand.ContainsKey)
                .Select(brand => byBrand[brand])
                .Take(RecentBrandLimit);
        }

        var bySlug = available.ToDictionary(
            metadata => metadata.BrandSlug,
            metadata => new CategoryInfo(
                metadata.Brand,
                metadata.BrandSlug,
                metadata.ImageUrl,
                AppConstants.Categories.Games,
                GameMetadata.ParseCategories(metadata.Categories)),
            StringComparer.OrdinalIgnoreCase);

        return orderedSlugs!
            .Where(bySlug.ContainsKey)
            .Select(slug => bySlug[slug])
            .Take(RecentBrandLimit);
    }

    public static List<string> ParsePopularGameSlugs(string? json)
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

    public async Task<IEnumerable<GameCategoryInfo>> GetGameCategoriesAsync()
    {
        var cached = await _cache.TryGetCacheAsync<List<GameCategoryInfo>>(RedisCacheKeys.GameCategories);
        if (cached is not null)
            return cached;

        var registry = GameCategoryRegistry.Parse(
            await _appSettings.GetAsync(AppConstants.SystemSettings.GameCategories));

        var merged = GameCategoryRegistry.Merge(
            registry,
            await CountCategoryUsageAsync(onlyWithActiveProduct: true),
            includeEmpty: false);

        var (total, uncategorized) = await CountActiveBrandsAsync();

        if (total == 0)
            return [];

        var result = new List<GameCategoryInfo>(merged.Count + 2)
        {
            new(AppConstants.StorefrontCategories.AllSlug,
                AppConstants.StorefrontCategories.AllName,
                total)
        };

        result.AddRange(merged);

        if (uncategorized > 0)
            result.Add(new GameCategoryInfo(
                AppConstants.StorefrontCategories.UncategorizedSlug,
                AppConstants.StorefrontCategories.UncategorizedName,
                uncategorized));

        await _cache.TrySetCacheAsync(RedisCacheKeys.GameCategories, result, 5);
        return result;
    }

    public async Task<GameDetailResponse?> GetGameDetailAsync(string brandSlug)
    {
        var slug = Slug.From(brandSlug);
        if (slug == null)
            return null;

        var metadataList = await _cache.TryGetGameMetadataListAsync();
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

    public async Task<Dictionary<string, int>> CountCategoryUsageAsync(bool onlyWithActiveProduct)
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
}
