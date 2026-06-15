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
    private readonly DigiflazzProvider _digiflazzProvider; // <-- Wadah baru untuk Digiflazz Provider

    // Constructor dengan 3 Injection (DbContext, R2, dan Digiflazz)
    public ProductService(
        DigitalinDbContext db, 
        CloudflareR2Provider r2Provider, 
        DigiflazzProvider digiflazzProvider)
    {
        _db = db;
        _r2Provider = r2Provider;
        _digiflazzProvider = digiflazzProvider;
    }

    // ─── MASTER LOGIKA SINKRONISASI (Satu untuk Semua) ───
    public async Task<(int Added, int Updated)> SyncCatalogFromDigiflazzAsync()
    {
        var dataArray = await _digiflazzProvider.GetPriceListAsync();

        int added = 0; int updated = 0;

        foreach (var item in dataArray)
        {
            var category = item.GetProperty(AppConstants.DigiflazzResponseKeys.Category).GetString();
            if (category != AppConstants.Categories.Games) continue;

            var sku = item.GetProperty(AppConstants.DigiflazzResponseKeys.BuyerSkuCode).GetString() ?? "";
            var name = item.GetProperty(AppConstants.DigiflazzResponseKeys.ProductName).GetString() ?? "";
            var modalPrice = item.GetProperty(AppConstants.DigiflazzResponseKeys.Price).GetDecimal(); 
            var brand = item.GetProperty(AppConstants.DigiflazzResponseKeys.Brand).GetString() ?? "";

            var existingMeta = await _db.GameMetadata.FirstOrDefaultAsync(m => m.Brand == brand);

            if (existingMeta == null)
            {
                _db.GameMetadata.Add(new GameMetadata 
                { 
                    Brand = brand, 
                    InputType = AppConstants.InputTypes.UserIdOnly
                });
                await _db.SaveChangesAsync(); 
            }
            
            var existingProduct = await _db.GameProducts.FirstOrDefaultAsync(p => p.BuyerSkuCode == sku);

            decimal marginYangDipakai = existingProduct?.CustomMargin ?? 0m;
            decimal sellingPrice = modalPrice + marginYangDipakai;

            if (existingProduct != null)
            {
                // Hanya update jika ada perubahan harga/nama asli biar hemat resource
                if (existingProduct.Price != sellingPrice || existingProduct.Name != name)
                {
                    existingProduct.Price = sellingPrice;
                    existingProduct.Name = name;
                    updated++;
                }
            }
            else
            {
                _db.GameProducts.Add(new GameProduct
                {
                    BuyerSkuCode = sku, Name = name, Price = sellingPrice,
                    Brand = brand, Category = category, GameCode = brand
                });
                added++;
            }
        }

        await _db.SaveChangesAsync();
        return (added, updated);
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

        // Tarik metadata untuk memberikan instruksi ke Frontend
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