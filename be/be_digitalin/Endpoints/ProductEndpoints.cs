using be_digitalin.Models;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using Microsoft.AspNetCore.Mvc;
using be_digitalin.Services.Games;
using be_digitalin.Services.Games.GameTemplates;
namespace be_digitalin.Endpoints;

public static class ProductEndpoints
{
    /// <summary>
    /// Batas atas ?limit di GET /games/brands. Satu section homepage hanya perlu
    /// satu batch kecil; angka di atas ini hanya menyeret ratusan kartu + ikonnya
    /// sekaligus tanpa ada yang membacanya.
    /// </summary>
    private const int MaxBrandPageSize = 60;

    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        // AREA PUBLIK
        var publicGroup = app.MapGroup("/games")
            .WithTags("Katalog Game (Public)");

        // GET /games/brands?category=&offset=&limit=
        // Tanpa query param = seluruh katalog (kompatibel dengan pemanggil lama).
        publicGroup.MapGet("/brands", async (
            [FromQuery] string? category,
            [FromQuery] int offset = 0,
            [FromQuery] int? limit = null,
            [FromServices] IProductService productService = null!) =>
        {
            if (offset < 0) offset = 0;
            if (limit < 1) limit = 1;
            if (limit > MaxBrandPageSize) limit = MaxBrandPageSize;

            var brands = await productService.GetUniqueGameBrandsAsync(category, offset, limit);
            return Results.Ok(brands);
        });

        // GET /games/brands/popular
        // Didaftarkan di bawah /brands tapi tetap sebelum /{brandSlug}: path
        // dua segmen, jadi tidak tertelan catch-all brand detail.
        publicGroup.MapGet("/brands/popular", async (
            [FromServices] IProductService productService) =>
        {
            var brands = await productService.GetPopularGameBrandsAsync();
            return Results.Ok(brands);
        });

        // GET /games/categories
        publicGroup.MapGet("/categories", async (
            [FromServices] IProductService productService) =>
        {
            var categories = await productService.GetGameCategoriesAsync();
            return Results.Ok(categories);
        });

        // GET /games/{brandSlug}
        publicGroup.MapGet("/{brandSlug}", async (
            string brandSlug,
            [FromServices] IProductService productService) =>
        {
            var game = await productService.GetGameDetailAsync(brandSlug);

            return game == null
                ? Results.NotFound(new { message = "Game tidak ditemukan" })
                : Results.Ok(game);
        });
        
        // AREA ADMIN 
        var adminGroup = app.MapGroup("/admin/products")
                            .WithTags("Manajemen Produk (Admin)")
                            .RequireAuthorization("AdminOnly"); 
        
        // GET /admin/products (PENCARIAN & PAGINASI) 
        adminGroup.MapGet("/", async (
            [FromQuery] string? search, 
            [FromQuery] string? brand,
            [FromQuery] int page = 1, 
            [FromQuery] int limit = 20, 
            [FromServices] IProductService productService = null!) => 
        {
            if (limit > 100) limit = 100;
            if (page < 1) page = 1;

            var result = await productService.GetPagedProductsAsync(
                search, 
                brand,
                page, 
                limit);
            return Results.Ok(result);
        });
        
        // POST /admin/products (BUAT PRODUK MANUAL)
        adminGroup.MapPost("/", async (GameProduct product, [FromServices] IProductService productService) =>
        {
            try
            {
                var createdProduct = await productService.CreateProductAsync(product);
                return Results.Created($"{AppConstants.Api.Prefix}/admin/products/{createdProduct.Id}", createdProduct);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
        
        // POST /admin/products/{id}/upload-icon (UPLOAD ICON KE CLOUDFLARE R2)
        adminGroup.MapPost("/{id}/upload-item-image", async (
            int id, 
            IFormFile file, 
            [FromServices] IProductService productService) =>
        {
            if (file == null || file.Length == 0) return Results.BadRequest("File tidak boleh kosong.");

            // tolak file yang lebih dari 2MB
            if (file.Length > AppConstants.FileLimits.MaxImageBytes)
                return Results.BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

            var imageUrl = await productService.UpdateProductIconAsync(id, file);
            if (imageUrl == null) return Results.NotFound(new { message = "Produk tidak ditemukan." });

            return Results.Ok(new { message = "Gambar item nominal berhasil diperbarui!", url = imageUrl });
        }).DisableAntiforgery();
        

        // AREA ADMIN
        var systemGroup = app.MapGroup("/admin/system")
                             .WithTags("Sistem & Integrasi (Admin)")
                             .RequireAuthorization("AdminOnly"); 

        // POST /admin/system/sync-digiflazz (SINKRONISASI MANUAL DARI TOMBOL ADMIN)
        systemGroup.MapPost("/sync-digiflazz", async ([FromServices] IProductService productService) =>
        {
            try
            {
                var (added, updated, deactivated) =
                    await productService.SyncCatalogFromDigiflazzAsync();

                return Results.Ok(new
                {
                    message = $"Sync selesai! {added} produk baru, {updated} produk diupdate, {deactivated} produk dinonaktifkan."
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
        
        // DELETE /admin/system/clean-dummy-products (HAPUS DATA DUMMY)
        systemGroup.MapDelete("/clean-dummy-products", async ([FromServices] IProductService productService) =>
        {
            var count = await productService.CleanDummyProductsAsync();

            return count == 0
                ? Results.Ok(new { message = "Database sudah bersih!" })
                : Results.Ok(new { message = $"Sapu bersih berhasil! {count} data dummy telah dihapus." });
        });

        // PUT /admin/products/{id}/margin (SET MARGIN KHUSUS PER ITEM)
        adminGroup.MapPut("/{id}/margin", async (int id, [FromBody] UpdateCustomMarginRequest req, [FromServices] IProductService productService) =>
        {
            var updated = await productService.UpdateProductMarginAsync(id, req.Margin);
            return updated
                ? Results.Ok(new { message = "Margin berhasil diatur!" })
                : Results.NotFound();
        });

        // GET /admin/system/midtrans-expiry
        systemGroup.MapGet("/midtrans-expiry", async ([FromServices] IProductService productService) =>
        {
            var currentExpiry = await productService.GetMidtransExpiryAsync();
            return Results.Ok(new { expiry = currentExpiry });
        });

        // PUT /admin/system/midtrans-expiry (UBAH WAKTU EXPIRY)
        systemGroup.MapPut("/midtrans-expiry", async ([FromServices] IProductService productService, [FromBody] UpdateExpiryRequest req) =>
        {
            await productService.SetMidtransExpiryAsync(req.Expiry);
            return Results.Ok(new { message = "Waktu kedaluwarsa Midtrans berhasil diubah!" });
        });

        // GET /admin/system/game-templates (DAFTAR PRESET TEMPLATE GAME - SSOT)
        systemGroup.MapGet("/game-templates", () =>
        {
            var templates = GameTemplateFactory.All
                .Select(template => new
                {
                    type = template.Type.ToString(),
                    displayName = template.DisplayName,
                    targetTemplate = template.TargetTemplate,
                    inputFields = template.InputFields
                });

            return Results.Ok(templates);
        });

        // GET /admin/system/game-categories (REGISTRY KATEGORI STOREFRONT)
        systemGroup.MapGet("/game-categories", async (
            [FromServices] IProductService productService) =>
        {
            var categories = await productService.GetGameCategoryRegistryAsync();
            return Results.Ok(categories);
        });

        // GET /admin/system/popular-games (GAME AKTIF + PILIHAN ADMIN)
        systemGroup.MapGet("/popular-games", async (
            [FromServices] IProductService productService) =>
        {
            var selected = await productService.GetPopularGameBrandsAsync();
            var options = await productService.GetPopularGameOptionsAsync();
            return Results.Ok(new { selected, options });
        });

        // PUT /admin/system/popular-games (GANTI SELURUH KURASI GAME POPULER)
        systemGroup.MapPut("/popular-games", async (
            [FromBody] UpdatePopularGamesRequest req,
            [FromServices] IProductService productService) =>
        {
            if (req.Slugs == null)
                return Results.BadRequest(new { message = "Daftar game populer wajib dikirim." });

            try
            {
                var selected = await productService.UpdatePopularGameBrandsAsync(req.Slugs);
                return Results.Ok(new { message = "Daftar game populer berhasil disimpan!", selected });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // PUT /admin/system/game-categories (URUTAN / RENAME / TAMBAH / HAPUS KATEGORI)
        systemGroup.MapPut("/game-categories", async (
            [FromBody] UpdateGameCategoriesRequest req,
            [FromServices] IProductService productService) =>
        {
            try
            {
                var categories = await productService.UpdateGameCategoryRegistryAsync(req);

                return Results.Ok(new
                {
                    message = "Kategori storefront berhasil disimpan!",
                    categories
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // GET /admin/system/metadata
        systemGroup.MapGet("/metadata", async (
            [FromServices] IProductService productService) =>
        {
            var metadata = await productService.GetGameMetadataAsync();
            return Results.Ok(metadata);
        });

        // PUT /admin/system/metadata/{id} 
        systemGroup.MapPut("/metadata/{id}", async (
            int id,
            [FromBody] UpdateGameMetadataRequest req,
            [FromServices] IProductService productService) =>
        {
            try
            {
                var metadata = await productService.UpdateGameMetadataAsync(id, req);

                return metadata == null
                    ? Results.NotFound(new { message = "Metadata tidak ditemukan." })
                    : Results.Ok(new
                    {
                        message = "Pengaturan game berhasil diperbarui!",
                        metadata
                    });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
        
        // POST /admin/system/metadata/{id}/upload-game-cover (UPLOAD GAMBAR COVER GAME UTAMA)
        systemGroup.MapPost("/metadata/{id}/upload-game-cover", async (
            int id,
            IFormFile file,
            [FromServices] IProductService productService) =>
        {
            if (file == null || file.Length == 0) return Results.BadRequest("File tidak boleh kosong.");

            // tolak jadi lebih dari 2MB
            if (file.Length > AppConstants.FileLimits.MaxImageBytes)
                return Results.BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

            var imageUrl = await productService.UpdateGameCoverAsync(id, file);
            if (imageUrl == null) return Results.NotFound(new { message = "Metadata Game tidak ditemukan." });

            return Results.Ok(new { message = "Gambar cover game berhasil diperbarui!", url = imageUrl });
        }).DisableAntiforgery();
    }
}

public record UpdateCustomMarginRequest(decimal? Margin);
public record UpdatePopularGamesRequest(IReadOnlyList<string>? Slugs);
public record UpdateExpiryRequest(int Expiry);
