using be_digitalin.Application.Ports;
using be_digitalin.Application.Validation;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Products;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Settings;

namespace be_digitalin.Services.Games;

/// <summary>
/// Compatibility facade untuk <see cref="IProductService"/> yang mendelegasikan
/// tanggung jawab ke layanan vertikal terfokus:
/// - <see cref="StorefrontCatalogService"/> (kueri storefront publik & cek username)
/// - <see cref="ProductSyncService"/> (sinkronisasi katalog Digiflazz)
/// - <see cref="AdminCatalogManagementService"/> (pengelolaan produk & metadata admin)
/// </summary>
public class ProductService : IProductService
{
    internal const int RecentBrandLimit = StorefrontCatalogService.RecentBrandLimit;

    private readonly IAppSettingsService _appSettings;
    private readonly StorefrontCatalogService _storefront;
    private readonly ProductSyncService _sync;
    private readonly AdminCatalogManagementService _admin;

    public ProductService(
        IApplicationDbContext db,
        ICloudflareR2Provider r2Provider,
        IDigiflazzProvider digiflazzProvider,
        IAppSettingsService appSettings,
        ILogger<ProductService> logger,
        IRedisService redis)
    {
        _appSettings = appSettings;
        var cache = new CatalogCacheHelper(db, redis, logger);
        _storefront = new StorefrontCatalogService(db, digiflazzProvider, appSettings, cache);
        _sync = new ProductSyncService(db, digiflazzProvider, cache);
        _admin = new AdminCatalogManagementService(db, r2Provider, appSettings, cache, _storefront);
    }

    public Task<IReadOnlyList<GameMetadataResponse>> GetGameMetadataAsync() =>
        _admin.GetGameMetadataAsync();

    public Task<GameMetadataResponse?> GetGameMetadataByIdAsync(int id) =>
        _admin.GetGameMetadataByIdAsync(id);

    public Task<GameMetadataResponse?> UpdateGameMetadataAsync(int id, UpdateGameMetadataRequest request) =>
        _admin.UpdateGameMetadataAsync(id, request);

    public Task<(int Added, int Updated, int Deactivated)> SyncCatalogFromDigiflazzAsync(CancellationToken cancellationToken = default) =>
        _sync.SyncCatalogFromDigiflazzAsync(cancellationToken);

    public Task<IEnumerable<ProductResponse>> GetAllProductsAsync() =>
        _admin.GetAllProductsAsync();

    public Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync(string? category = null, int offset = 0, int? limit = null) =>
        _storefront.GetUniqueGameBrandsAsync(category, offset, limit);

    public Task<IEnumerable<CategoryInfo>> GetPopularGameBrandsAsync() =>
        _storefront.GetPopularGameBrandsAsync();

    public Task<IEnumerable<CategoryInfo>> GetRecentGameBrandsAsync(int? customerId, IReadOnlyList<string>? guestSlugs = null) =>
        _storefront.GetRecentGameBrandsAsync(customerId, guestSlugs);

    public Task<IEnumerable<GameMetadataResponse>> GetPopularGameOptionsAsync() =>
        _admin.GetPopularGameOptionsAsync();

    public Task<IEnumerable<CategoryInfo>> UpdatePopularGameBrandsAsync(IReadOnlyList<string> slugs) =>
        _admin.UpdatePopularGameBrandsAsync(slugs);

    public Task<IEnumerable<GameCategoryInfo>> GetGameCategoriesAsync() =>
        _storefront.GetGameCategoriesAsync();

    public Task<IEnumerable<GameCategoryInfo>> GetGameCategoryRegistryAsync() =>
        _admin.GetGameCategoryRegistryAsync();

    public Task<IEnumerable<GameCategoryInfo>> UpdateGameCategoryRegistryAsync(UpdateGameCategoriesRequest request) =>
        _admin.UpdateGameCategoryRegistryAsync(request);

    public Task<GameDetailResponse?> GetGameDetailAsync(string brandSlug) =>
        _storefront.GetGameDetailAsync(brandSlug);

    public Task<UsernameCheckResponse> CheckUsernameAsync(string brandSlug, Dictionary<string, string?> targetValues, CancellationToken cancellationToken = default) =>
        _storefront.CheckUsernameAsync(brandSlug, targetValues, cancellationToken);

    public Task<PagedResult<GameProduct>> GetPagedProductsAsync(string? search, string? brand, int page, int limit) =>
        _admin.GetPagedProductsAsync(search, brand, page, limit);

    public Task<GameProduct> CreateProductAsync(GameProduct product) =>
        _admin.CreateProductAsync(product);

    public Task<string?> UpdateProductIconAsync(int productId, ImageFileData file, CancellationToken cancellationToken = default) =>
        _admin.UpdateProductIconAsync(productId, file, cancellationToken);

    public Task<int> CleanDummyProductsAsync() =>
        _admin.CleanDummyProductsAsync();

    public Task<bool> UpdateProductMarginAsync(int productId, decimal? margin) =>
        _admin.UpdateProductMarginAsync(productId, margin);

    public async Task<int> GetMidtransExpiryAsync()
    {
        var value = await _appSettings.GetAsync(AppConstants.SystemSettings.MidtransExpiry);
        return int.TryParse(value, out var minutes) ? minutes : 5;
    }

    public async Task SetMidtransExpiryAsync(int expiryMinutes)
    {
        await _appSettings.SetAsync(
            AppConstants.SystemSettings.MidtransExpiry,
            expiryMinutes.ToString());
    }

    public Task<string?> UpdateGameCoverAsync(int metadataId, ImageFileData file, CancellationToken cancellationToken = default) =>
        _admin.UpdateGameCoverAsync(metadataId, file, cancellationToken);

    internal static string? NormalizeCategory(string? category) =>
        Slug.From(category);

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

    internal static List<CategoryInfo> PageBrands(
        IEnumerable<CategoryInfo> brands,
        string? category,
        int offset,
        int? limit) =>
        StorefrontCatalogService.PageBrands(brands, category, offset, limit);

    internal static List<string> ParsePopularGameSlugs(string? json) =>
        StorefrontCatalogService.ParsePopularGameSlugs(json);

    internal static string ValidateTargetTemplate(
        string targetTemplate,
        IReadOnlyList<GameInputField> inputFields) =>
        InputSchemaRules.ValidateTargetTemplate(targetTemplate, inputFields);
}
