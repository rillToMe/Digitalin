using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Constants;
using be_digitalin.Providers;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services;

public class ProductService : IProductService
{
    private readonly DigitalinDbContext _db;
    private readonly CloudflareR2Provider _r2Provider;
    private readonly DigiflazzProvider _digiflazzProvider;
    
    public ProductService(
        DigitalinDbContext db, 
        CloudflareR2Provider r2Provider, 
        DigiflazzProvider digiflazzProvider)
    {
        _db = db;
        _r2Provider = r2Provider;
        _digiflazzProvider = digiflazzProvider;
    }

    // logic sync
    public async Task<(int Added, int Updated, int Deactivated)> SyncCatalogFromDigiflazzAsync()
{
    var dataArray = await _digiflazzProvider.GetPriceListAsync();

    int added = 0;
    int updated = 0;
    int deactivated = 0;

    var activeDigiflazzSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        var existingMeta = await _db.GameMetadata
            .FirstOrDefaultAsync(m => m.Brand == brand);

        if (existingMeta == null)
        {
            _db.GameMetadata.Add(new GameMetadata
            {
                Brand = brand,
                InputType = AppConstants.InputTypes.UserIdOnly
            });
        }

        var existingProduct = await _db.GameProducts
            .FirstOrDefaultAsync(p => p.BuyerSkuCode == sku);

        if (existingProduct != null)
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
            _db.GameProducts.Add(new GameProduct
            {
                BuyerSkuCode = sku,
                Name = name,
                Price = modalPrice,
                Brand = brand,
                Category = category,
                GameCode = brand,
                IsActive = true
            });

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
    
    // HOMEPAGE: Menampilkan Brand beserta Instruksi Form UI-nya
    public async Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync()
    {
        var groupedGames = await _db.GameProducts
            .Where(p => p.Category == AppConstants.Categories.Games && !string.IsNullOrEmpty(p.Brand))
            .GroupBy(p => p.Brand)
            .Select(g => new 
            {
                Brand = g.Key,
                ImageUrl = g.First().ImageUrl,
                Category = g.First().Category
            })
            .ToListAsync();

        // Ambil metadata untuk memberikan instruksi ke FE
        var metadataList = await _db.GameMetadata.ToListAsync();

        var result = groupedGames.Select(g => 
        {
            var meta = metadataList.FirstOrDefault(m => m.Brand.ToLower() == g.Brand.ToLower());
            
            return new CategoryInfo(
                g.Brand,
                g.Brand.ToLower().Replace(" ", "-"),
                g.ImageUrl,
                g.Category,
                meta?.InputType ?? AppConstants.InputTypes.UserIdOnly
            );
        }).OrderBy(c => c.Brand).ToList();

        return result;
    }
    
    public async Task<IEnumerable<GameProduct>> GetProductsByBrandAsync(string brand)
    {
        var dbBrandName = brand.Replace("-", " ").ToLower();
        return await _db.GameProducts
            .Where(p => p.Brand.ToLower() == dbBrandName)
            .OrderBy(p => p.Price) 
            .ToListAsync();
    }
    
    public async Task<PagedResult<GameProduct>> GetPagedProductsAsync(string? search, int page, int limit)
    {
        var query = _db.GameProducts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s) || p.Brand.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Name) 
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        int totalPages = (int)Math.Ceiling(totalCount / (double)limit);
        return new PagedResult<GameProduct>(items, totalCount, totalPages, page);
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
}