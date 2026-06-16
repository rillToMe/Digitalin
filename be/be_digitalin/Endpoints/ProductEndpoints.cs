using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using be_digitalin.Config;
using be_digitalin.Data;
using be_digitalin.Models;
using be_digitalin.Constants;
using be_digitalin.Services;
using Microsoft.AspNetCore.Mvc; 
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace be_digitalin.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. AREA PUBLIK (Katalog Game Pembeli)
        var publicGroup = app.MapGroup("/games")
            .WithTags("Katalog Game (Public)");
        
        // GET /games/brands - Untuk nampilin list game di Homepage
        publicGroup.MapGet("/brands", async ([FromServices] DigitalinDbContext db) =>
        {
            var brands = await db.GameMetadata
                .Select(m => new {
                    m.Id,
                    m.Brand,
                    Slug = m.Brand.ToLower().Replace(" ", "-"), // Bikin slug otomatis
                    m.ImageUrl, 
                    m.InputType
                })
                .OrderBy(m => m.Brand)
                .ToListAsync();

            return Results.Ok(brands);
        });

        // GET /games/{brandSlug} - Untuk nampilin detail game + list diamond
        publicGroup.MapGet("/{brandSlug}", async (string brandSlug, [FromServices] DigitalinDbContext db) =>
        {
            // 1. Cari metadata gamenya dulu berdasarkan slug
            var meta = await db.GameMetadata
                .FirstOrDefaultAsync(m => m.Brand.ToLower().Replace(" ", "-") == brandSlug);

            if (meta == null) return Results.NotFound(new { message = "Game tidak ditemukan" });

            // 2. Tarik daftar produk (diamond) untuk game ini
            var products = await db.GameProducts
                .Where(p => p.Brand == meta.Brand) 
                .OrderBy(p => p.Price)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.GameCode,
                    p.Brand,
                    p.Category,
                    p.ImageUrl,
                    p.CustomMargin
                })
                .ToListAsync();

            // 3. Gabungkan jadi 1 response sesuai format GameDetailResponse di api.ts
            return Results.Ok(new {
                brand = meta.Brand,
                gameCode = meta.Brand, 
                inputType = meta.InputType, // Kirim input type langsung dari sini
                imageUrl = meta.ImageUrl,   // Ini gambar COVER utama
                products = products         // Ini list pilihan diamond-nya
            });
        });
        

        // 2. AREA ADMIN (Manajemen Produk)
        var adminGroup = app.MapGroup("/admin/products")
                            .WithTags("Manajemen Produk (Admin)")
                            .RequireAuthorization("AdminOnly"); // <-- Semua endpoint admin wajib role ADMIN
        
        // GET /admin/products (PENCARIAN & PAGINASI) 
        adminGroup.MapGet("/", async (
            [FromQuery] string? search, 
            [FromQuery] int page = 1, 
            [FromQuery] int limit = 20, 
            [FromServices] IProductService productService = null!) => 
        {
            if (limit > 100) limit = 100;
            if (page < 1) page = 1;

            var result = await productService.GetPagedProductsAsync(search, page, limit);
            return Results.Ok(result);
        });
        
        // POST /admin/products (BUAT PRODUK MANUAL)
        adminGroup.MapPost("/", async (GameProduct product, [FromServices] IProductService productService) =>
        {
            var createdProduct = await productService.CreateProductAsync(product);
            return Results.Created($"/admin/products/{createdProduct.Id}", createdProduct);
        });
        
        // POST /admin/products/{id}/upload-icon (UPLOAD ICON KE CLOUDFLARE R2)
        adminGroup.MapPost("/{id}/upload-item-image", async (
            int id, 
            IFormFile file, 
            [FromServices] IProductService productService) =>
        {
            if (file == null || file.Length == 0) return Results.BadRequest("File tidak boleh kosong.");

            // [M5] Reject files larger than 2 MB
            if (file.Length > AppConstants.FileLimits.MaxImageBytes)
                return Results.BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

            var imageUrl = await productService.UpdateProductIconAsync(id, file);
            if (imageUrl == null) return Results.NotFound(new { message = "Produk tidak ditemukan." });

            return Results.Ok(new { message = "Gambar item nominal berhasil diperbarui!", url = imageUrl });
        }).DisableAntiforgery();
        

        // 3. AREA ADMIN (Sistem & Integrasi)
        var systemGroup = app.MapGroup("/admin/system")
                             .WithTags("Sistem & Integrasi (Admin)")
                             .RequireAuthorization("AdminOnly"); // <-- Wajib role ADMIN

        // POST /admin/system/sync-digiflazz (SINKRONISASI MANUAL DARI TOMBOL ADMIN)
        systemGroup.MapPost("/sync-digiflazz", async ([FromServices] IProductService productService) =>
        {
            // Panggil fungsi master di ProductService
            var (added, updated) = await productService.SyncCatalogFromDigiflazzAsync();
            
            return Results.Ok(new { message = $"Sync selesai! {added} produk baru, {updated} produk diupdate." });
        });
        
        // DELETE /admin/system/clean-dummy-products (HAPUS DATA DUMMY)
        systemGroup.MapDelete("/clean-dummy-products", async ([FromServices] DigitalinDbContext db) =>
        {
            var dummyProducts = await db.GameProducts.Where(p => string.IsNullOrEmpty(p.BuyerSkuCode)).ToListAsync();

            if (!dummyProducts.Any()) return Results.Ok(new { message = "Database sudah bersih!" });

            int count = dummyProducts.Count;
            db.GameProducts.RemoveRange(dummyProducts);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = $"Sapu bersih berhasil! {count} data dummy telah dihapus." });
        });
        
        // PUT /admin/products/{id}/margin (SET MARGIN KHUSUS PER ITEM)
        adminGroup.MapPut("/{id}/margin", async (int id, [FromBody] UpdateCustomMarginRequest req, [FromServices] DigitalinDbContext db) =>
        {
            var product = await db.GameProducts.FindAsync(id);
            if (product == null) return Results.NotFound();

            decimal oldMargin = product.CustomMargin ?? 0m;
            decimal modalPrice = product.Price - oldMargin;

            decimal newMargin = req.Margin ?? 0m;

            product.Price = modalPrice + newMargin;            
            product.CustomMargin = req.Margin; // <-- Ambil Margin dari 'req'
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Margin berhasil diatur!" });
        });
        
        // GET /admin/system/midtrans-expiry (BACA WAKTU EXPIRY SAAT INI)
        systemGroup.MapGet("/midtrans-expiry", async ([FromServices] DigitalinDbContext db) =>
        {
            var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == AppConstants.SystemSettings.MidtransExpiry);
            int currentExpiry = setting != null ? int.Parse(setting.Value) : 5; // Default 5 menit
            return Results.Ok(new { expiry = currentExpiry });
        });

        // PUT /admin/system/midtrans-expiry (UBAH WAKTU EXPIRY)
        systemGroup.MapPut("/midtrans-expiry", async ([FromServices] DigitalinDbContext db, [FromBody] UpdateExpiryRequest req) =>
        {
            var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == AppConstants.SystemSettings.MidtransExpiry);
            
            if (setting == null)
            {
                db.AppSettings.Add(new AppSetting { Key = AppConstants.SystemSettings.MidtransExpiry, Value = req.Expiry.ToString() });
            }
            else
            {
                setting.Value = req.Expiry.ToString();
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Waktu kedaluwarsa Midtrans berhasil diubah!" });
        });
        
        // GET /admin/system/metadata - (Tarik semua data buat ditampilin di Tabel Admin)
        systemGroup.MapGet("/metadata", async ([FromServices] DigitalinDbContext db) =>
        {
            var metadata = await db.GameMetadata.OrderBy(m => m.Brand).ToListAsync();
            return Results.Ok(metadata);
        });

        // PUT /admin/system/metadata/{id} - (Simpan perubahan tipe input & server dari Admin)
        systemGroup.MapPut("/metadata/{id}", async (int id, [FromBody] UpdateMetadataRequest req, [FromServices] DigitalinDbContext db) =>
        {
            var meta = await db.GameMetadata.FindAsync(id);
            if (meta == null) return Results.NotFound(new { message = "Metadata tidak ditemukan." });

            meta.InputType = req.InputType;
            meta.ServerList = req.ServerList; // Simpan daftar server baru
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Pengaturan game berhasil diperbarui!" });
        });
        
        // POST /admin/system/metadata/{id}/upload-game-cover (UPLOAD GAMBAR COVER GAME UTAMA)
        systemGroup.MapPost("/metadata/{id}/upload-game-cover", async (
            int id, 
            IFormFile file, 
            [FromServices] DigitalinDbContext db,
            [FromServices] be_digitalin.Providers.CloudflareR2Provider r2) =>
        {
            if (file == null || file.Length == 0) return Results.BadRequest("File tidak boleh kosong.");

            // [M5] Reject files larger than 2 MB
            if (file.Length > AppConstants.FileLimits.MaxImageBytes)
                return Results.BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

            var meta = await db.GameMetadata.FindAsync(id);
            if (meta == null) return Results.NotFound(new { message = "Metadata Game tidak ditemukan." });

            // ==========================================
            // TAMBAHAN BARU: HAPUS GAMBAR LAMA DULU
            // ==========================================
            if (!string.IsNullOrEmpty(meta.ImageUrl))
            {
                await r2.DeleteFileAsync(meta.ImageUrl); 
            }

            // UBAH FOLDER UPLOADNYA DI SINI
            // Ganti AppConstants.StorageFolders.Icons menjadi AppConstants.StorageFolders.GameCovers (atau langsung "icon-cover")
            var imageUrl = await r2.UploadFileAsync(file, "icon-cover"); 

            meta.ImageUrl = imageUrl;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Gambar cover game berhasil diperbarui!", url = imageUrl });
        }).DisableAntiforgery();
    }
}

public record UpdateCustomMarginRequest(decimal? Margin);
public record UpdateExpiryRequest(int Expiry);
public record UpdateMetadataRequest(string InputType, string? ServerList);
