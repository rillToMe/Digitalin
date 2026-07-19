using be_digitalin.DTOs;
using be_digitalin.Models;

namespace be_digitalin.Services;

public interface IProductService
{
    
    Task<PagedResult<GameProduct>> GetPagedProductsAsync(
        string? search,
        string? brand,
        int page,
        int limit);

    Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync();

    Task<IEnumerable<ProductResponse>> GetAllProductsAsync();

    Task<GameProduct> CreateProductAsync(GameProduct product);

    Task<string?> UpdateProductIconAsync(int id, IFormFile file);

    Task<IEnumerable<GameProduct>> GetProductsByBrandAsync(string brand);

    Task<(int Added, int Updated, int Deactivated)>
        SyncCatalogFromDigiflazzAsync();

    Task<IReadOnlyList<GameMetadataResponse>>
        GetGameMetadataAsync();

    Task<GameMetadataResponse?>
        GetGameMetadataByIdAsync(int id);
    
    Task<GameDetailResponse?> 
        GetGameDetailAsync(string brandSlug);
    

    Task<GameMetadataResponse?>
        UpdateGameMetadataAsync(
            int id,
            UpdateGameMetadataRequest request);

    /// <summary>Hapus produk dummy (tanpa BuyerSkuCode). Mengembalikan jumlah yang dihapus.</summary>
    Task<int> CleanDummyProductsAsync();

    /// <summary>Set custom margin per produk & recalculate harga jual. Null jika produk tidak ditemukan.</summary>
    Task<bool> UpdateProductMarginAsync(int productId, decimal? margin);

    /// <summary>Baca durasi expiry Midtrans (menit) dari AppSettings. Default 5.</summary>
    Task<int> GetMidtransExpiryAsync();

    /// <summary>Ubah durasi expiry Midtrans (menit) di AppSettings.</summary>
    Task SetMidtransExpiryAsync(int expiryMinutes);

    /// <summary>Upload/replace gambar cover game (GameMetadata). Null jika metadata tidak ditemukan.</summary>
    Task<string?> UpdateGameCoverAsync(int metadataId, IFormFile file);
}