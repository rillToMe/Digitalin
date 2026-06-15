using be_digitalin.Constants;
using be_digitalin.Services;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Endpoints;

public static class BannerEndpoints
{
    public static void MapBannerEndpoints(this IEndpointRouteBuilder app)
    {
        // =======================================================
        // 1. AREA PUBLIK (Tampil di Frontend Pembeli)
        // =======================================================
        var publicGroup = app.MapGroup("/banners")
                             .WithTags("Banner Promosi (Public)");
        
        // GET /banners -> Mengambil semua banner aktif
        publicGroup.MapGet("/", async ([FromServices] IBannerService bannerService) =>
        {
            var banners = await bannerService.GetActiveBannersAsync();
            return Results.Ok(banners);
        });
        

        // =======================================================
        // 2. AREA ADMIN (Manajemen Banner)
        // =======================================================
        var adminGroup = app.MapGroup("/admin/banners")
                            .WithTags("Manajemen Banner (Admin)")
                            .RequireAuthorization("AdminOnly"); // <-- Wajib role ADMIN

        // POST /admin/banners/upload -> Upload Banner Baru
        adminGroup.MapPost("/upload", async (
            [FromForm] string title, 
            IFormFile file, 
            [FromServices] IBannerService bannerService) =>
        {
            if (file == null || file.Length == 0) return Results.BadRequest("File tidak boleh kosong.");

            // [M5] Reject files larger than 2 MB
            if (file.Length > AppConstants.FileLimits.MaxImageBytes)
                return Results.BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");
            
            var banner = await bannerService.CreateBannerAsync(title, file);
            return Results.Created($"/admin/banners/{banner.Id}", banner);
        }).DisableAntiforgery();
        
        // PUT /admin/banners/{id}/toggle -> Nyala/Matikan banner
        adminGroup.MapPut("/{id}/toggle", async (int id, [FromServices] IBannerService bannerService) =>
        {
            var exists = await bannerService.ToggleBannerStatusAsync(id);
            return Results.Ok(new { message = "Status banner berhasil diperbarui." });
        }).DisableAntiforgery();
        
        // DELETE /admin/banners/{id} -> Hapus Permanen
        adminGroup.MapDelete("/{id}", async (int id, [FromServices] IBannerService bannerService) =>
        {
            var success = await bannerService.DeleteBannerAsync(id);
            if (!success) return Results.NotFound(new { message = "Banner tidak ditemukan." });

            return Results.Ok(new { message = "Banner dan file gambar berhasil dibumihanguskan!" });
        });
    }
}