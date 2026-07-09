using be_digitalin.DTOs;
using be_digitalin.Models;

namespace be_digitalin.Services;

public interface IProductService
{
    Task<PagedResult<GameProduct>> GetPagedProductsAsync(string? search, int page, int limit);
    Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync();

    Task<IEnumerable<ProductResponse>> GetAllProductsAsync();
    Task<GameProduct> CreateProductAsync(GameProduct product);
    Task<string?> UpdateProductIconAsync(int id, IFormFile file);
    
    Task<IEnumerable<GameProduct>> GetProductsByBrandAsync(string brand);
    Task<(int Added, int Updated, int Deactivated)> SyncCatalogFromDigiflazzAsync();
}