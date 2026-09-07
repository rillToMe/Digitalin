using be_digitalin.Application.Validation;
using be_digitalin.DTOs;
using be_digitalin.Domain.Entities;
using be_digitalin.Services.Orders;

namespace be_digitalin.Services.Games;

public interface IProductService
{
    
    Task<PagedResult<GameProduct>> GetPagedProductsAsync(
        string? search,
        string? brand,
        int page,
        int limit);

    /// <summary>
    /// Brand katalog homepage. Semua argumen opsional: tanpa argumen = seluruh
    /// katalog; <paramref name="category"/> null/"all" = tanpa filter, "lainnya" =
    /// brand yang belum dikategorikan.
    /// </summary>
    Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync(
        string? category = null,
        int offset = 0,
        int? limit = null);

    /// <summary>
    /// Brand pilihan admin untuk section Populer homepage.
    /// </summary>
    Task<IEnumerable<CategoryInfo>> GetPopularGameBrandsAsync();

    Task<IEnumerable<GameMetadataResponse>> GetPopularGameOptionsAsync();

    Task<IEnumerable<CategoryInfo>> UpdatePopularGameBrandsAsync(
        IReadOnlyList<string> slugs);

    Task<IEnumerable<GameCategoryInfo>> GetGameCategoriesAsync();

    /// <summary>Registry kategori storefront untuk admin — termasuk kategori tanpa game.</summary>
    Task<IEnumerable<GameCategoryInfo>> GetGameCategoryRegistryAsync();

    /// <summary>
    /// Ganti seluruh registry kategori (urutan, nama, tambah, hapus).
    /// Melempar <see cref="InvalidOperationException"/> bila payload tidak valid
    /// atau ada kategori dihapus yang masih dipakai game.
    /// </summary>
    Task<IEnumerable<GameCategoryInfo>> UpdateGameCategoryRegistryAsync(
        UpdateGameCategoriesRequest request);

    Task<IEnumerable<ProductResponse>> GetAllProductsAsync();

    Task<GameProduct> CreateProductAsync(GameProduct product);

    Task<string?> UpdateProductIconAsync(int id, ImageFileData file, CancellationToken cancellationToken = default);

    Task<(int Added, int Updated, int Deactivated)>
        SyncCatalogFromDigiflazzAsync(CancellationToken cancellationToken = default);

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
    Task<string?> UpdateGameCoverAsync(int metadataId, ImageFileData file, CancellationToken cancellationToken = default);
}

