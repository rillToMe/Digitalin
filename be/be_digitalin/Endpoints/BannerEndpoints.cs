using be_digitalin.Constants;
using Microsoft.AspNetCore.Mvc;
using be_digitalin.Services.Content;

namespace be_digitalin.Endpoints;

public static class BannerEndpoints
{
    public static void MapBannerEndpoints(this IEndpointRouteBuilder app)
    {
        // AREA PUBLIK
        var publicGroup = app.MapGroup("/banners")
                             .WithTags("Banner Promosi (Public)");
        
        // GET /banners 
        publicGroup.MapGet("/", async ([FromServices] IBannerService bannerService) =>
        {
            var banners = await bannerService.GetActiveBannersAsync();
            return Results.Ok(banners);
        });
        

        // AREA ADMIN
        var adminGroup = app.MapGroup("/admin/banners")
                            .WithTags("Manajemen Banner (Admin)")
                            .RequireAuthorization("AdminOnly");

        // GET /admin/banners
        adminGroup.MapGet("/", async (
            [FromServices] IBannerService bannerService) =>
        {
            var banners = await bannerService.GetAllBannersAsync();
            return Results.Ok(banners);
        });
        
        // POST /admin/banners/upload
        adminGroup.MapPost("/upload", async (
            [FromForm] string title,
            IFormFile file,
            [FromServices] IBannerService bannerService,
            CancellationToken cancellationToken) =>
        {
            if (file == null || file.Length == 0) return Results.BadRequest("File tidak boleh kosong.");

            // tidak boleh upload lebih dari 2MB (cloudflare mahal bjir)
            if (file.Length > AppConstants.FileLimits.MaxImageBytes)
                return Results.BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

            try
            {
                var banner = await bannerService.CreateBannerAsync(title, file, cancellationToken);
                return Results.Created($"{AppConstants.Api.Prefix}/admin/banners/{banner.Id}", banner);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).DisableAntiforgery();
        
        // PUT /admin/banners/{id}/toggle = Nyala/Matikan banner
        adminGroup.MapPut("/{id}/toggle", async (int id, [FromServices] IBannerService bannerService) =>
        {
            var exists = await bannerService.ToggleBannerStatusAsync(id);
            return Results.Ok(new { message = "Status banner berhasil diperbarui." });
        }).DisableAntiforgery();
        
        // DELETE /admin/banners/{id}
        adminGroup.MapDelete("/{id}", async (int id, [FromServices] IBannerService bannerService, CancellationToken cancellationToken) =>
        {
            var success = await bannerService.DeleteBannerAsync(id, cancellationToken);
            if (!success) return Results.NotFound(new { message = "Banner tidak ditemukan." });

            return Results.Ok(new { message = "Banner dan file gambar berhasil dibumihanguskan!" });
        });
    }
}