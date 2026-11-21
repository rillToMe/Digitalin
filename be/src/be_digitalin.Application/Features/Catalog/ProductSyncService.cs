using be_digitalin.Application.Ports;
using be_digitalin.Application.Serialization;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Games.GameTemplates;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Games;

/// <summary>
/// Use case sinkronisasi katalog harga dan metadata brand dari supplier Digiflazz.
/// </summary>
public sealed class ProductSyncService
{
    private readonly IApplicationDbContext _db;
    private readonly IDigiflazzProvider _digiflazzProvider;
    private readonly CatalogCacheHelper _cache;

    public ProductSyncService(
        IApplicationDbContext db,
        IDigiflazzProvider digiflazzProvider,
        CatalogCacheHelper cache)
    {
        _db = db;
        _digiflazzProvider = digiflazzProvider;
        _cache = cache;
    }

    public async Task<(int Added, int Updated, int Deactivated)> SyncCatalogFromDigiflazzAsync(
        CancellationToken cancellationToken = default)
    {
        var dataArray = await _digiflazzProvider.GetPriceListAsync(cancellationToken);

        int added = 0;
        int updated = 0;
        int deactivated = 0;

        var activeDigiflazzSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var metadataByBrand = (await _db.GameMetadata.ToListAsync(cancellationToken))
            .GroupBy(m => m.Brand, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var products = await _db.GameProducts.ToListAsync(cancellationToken);
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

            var brandSlug = Slug.From(brand);
            if (brandSlug == null)
                continue;

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
                metadataByBrand[brand] = metadata;
            }

            if (metadata.BrandSlug != brandSlug)
                metadata.BrandSlug = brandSlug;

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
            .ToListAsync(cancellationToken);

        foreach (var product in productsToDeactivate)
        {
            product.IsActive = false;
            deactivated++;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Sync dibatalkan: katalog sedang ditulis proses sync lain " +
                "(BuyerSkuCode duplikat ditolak database). Coba lagi setelah sync berjalan selesai.",
                ex);
        }

        await _cache.TryInvalidateCacheAsync(
            RedisCacheKeys.GameMetadata,
            RedisCacheKeys.PopularGames,
            RedisCacheKeys.GameCategories,
            RedisCacheKeys.GameCategoryRegistry);

        return (added, updated, deactivated);
    }
}
