using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Games;
using be_digitalin.Services.Games.GameTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 4: migrasi seluruh /admin/system (12 endpoint) dari Minimal API
/// (ProductEndpoints.cs) ke Controller. File ProductEndpoints.cs dihapus.
/// Mapping 1:1 — service & kontrak tidak berubah.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/system")]
[Tags("Sistem & Integrasi (Admin)")]
public class AdminSystemController : ControllerBase
{
    private readonly IProductService _productService;

    public AdminSystemController(IProductService productService)
    {
        _productService = productService;
    }

    // POST /api/v1/admin/system/sync-digiflazz — sinkronisasi manual
    [HttpPost("sync-digiflazz")]
    public async Task<IActionResult> SyncDigiflazz(CancellationToken cancellationToken)
    {
        try
        {
            var (added, updated, deactivated) =
                await _productService.SyncCatalogFromDigiflazzAsync(cancellationToken);

            return Ok(new MessageResponse(
                $"Sync selesai! {added} produk baru, {updated} produk diupdate, {deactivated} produk dinonaktifkan."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // DELETE /api/v1/admin/system/clean-dummy-products — hapus data dummy
    [HttpDelete("clean-dummy-products")]
    public async Task<IActionResult> CleanDummyProducts()
    {
        var count = await _productService.CleanDummyProductsAsync();

        return count == 0
            ? Ok(new MessageResponse("Database sudah bersih!"))
            : Ok(new MessageResponse($"Sapu bersih berhasil! {count} data dummy telah dihapus."));
    }

    // GET /api/v1/admin/system/midtrans-expiry
    [HttpGet("midtrans-expiry")]
    public async Task<ActionResult<MidtransExpiryResponse>> GetMidtransExpiry()
    {
        var currentExpiry = await _productService.GetMidtransExpiryAsync();
        return Ok(new MidtransExpiryResponse(currentExpiry));
    }

    // PUT /api/v1/admin/system/midtrans-expiry — ubah waktu expiry
    [HttpPut("midtrans-expiry")]
    public async Task<IActionResult> SetMidtransExpiry([FromBody] UpdateExpiryRequest req)
    {
        await _productService.SetMidtransExpiryAsync(req.Expiry);
        return Ok(new MessageResponse("Waktu kedaluwarsa Midtrans berhasil diubah!"));
    }

    // GET /api/v1/admin/system/game-templates — daftar preset template game (SSOT)
    [HttpGet("game-templates")]
    public ActionResult<IEnumerable<GameTemplateResponse>> GetGameTemplates()
    {
        var templates = GameTemplateFactory.All
            .Select(template => new GameTemplateResponse(
                Type:           template.Type.ToString(),
                DisplayName:    template.DisplayName,
                TargetTemplate: template.TargetTemplate,
                InputFields:    template.InputFields));

        return Ok(templates);
    }

    // GET /api/v1/admin/system/game-categories — registry kategori storefront
    [HttpGet("game-categories")]
    public async Task<ActionResult<IEnumerable<GameCategoryInfo>>> GetGameCategories()
    {
        var categories = await _productService.GetGameCategoryRegistryAsync();
        return Ok(categories);
    }

    // GET /api/v1/admin/system/popular-games — game aktif + pilihan admin
    [HttpGet("popular-games")]
    public async Task<ActionResult<PopularGamesResponse>> GetPopularGames()
    {
        var selected = await _productService.GetPopularGameBrandsAsync();
        var options = await _productService.GetPopularGameOptionsAsync();
        return Ok(new PopularGamesResponse(selected, options));
    }

    // PUT /api/v1/admin/system/popular-games — ganti seluruh kurasi game populer
    [HttpPut("popular-games")]
    public async Task<IActionResult> UpdatePopularGames([FromBody] UpdatePopularGamesRequest req)
    {
        if (req.Slugs == null)
            return BadRequest(new MessageResponse("Daftar game populer wajib dikirim."));

        try
        {
            var selected = await _productService.UpdatePopularGameBrandsAsync(req.Slugs);
            return Ok(new PopularGamesSavedResponse("Daftar game populer berhasil disimpan!", selected));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // PUT /api/v1/admin/system/game-categories — urutan / rename / tambah / hapus
    [HttpPut("game-categories")]
    public async Task<IActionResult> UpdateGameCategories([FromBody] UpdateGameCategoriesRequest req)
    {
        try
        {
            var categories = await _productService.UpdateGameCategoryRegistryAsync(req);

            return Ok(new GameCategoriesSavedResponse("Kategori storefront berhasil disimpan!", categories));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // GET /api/v1/admin/system/metadata
    [HttpGet("metadata")]
    public async Task<ActionResult<IReadOnlyList<GameMetadataResponse>>> GetGameMetadata()
    {
        var metadata = await _productService.GetGameMetadataAsync();
        return Ok(metadata);
    }

    // PUT /api/v1/admin/system/metadata/{id}
    [HttpPut("metadata/{id}")]
    public async Task<IActionResult> UpdateGameMetadata(int id, [FromBody] UpdateGameMetadataRequest req)
    {
        try
        {
            var metadata = await _productService.UpdateGameMetadataAsync(id, req);

            return metadata == null
                ? NotFound(new MessageResponse("Metadata tidak ditemukan."))
                : Ok(new GameMetadataSavedResponse("Pengaturan game berhasil diperbarui!", metadata));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // POST /api/v1/admin/system/metadata/{id}/upload-game-cover — upload cover utama
    [HttpPost("metadata/{id}/upload-game-cover")]
    // file nullable: kalau bagian file tidak dikirim, MVC tidak auto-400
    // melainkan masuk ke guard di bawah — sama seperti Minimal API lama.
    public async Task<IActionResult> UploadGameCover(
        int id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("File tidak boleh kosong.");

        // tolak file lebih dari 2MB
        if (file.Length > AppConstants.FileLimits.MaxImageBytes)
            return BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

        try
        {
            var imageUrl = await _productService.UpdateGameCoverAsync(id, file, cancellationToken);
            if (imageUrl == null) return NotFound(new MessageResponse("Metadata Game tidak ditemukan."));

            return Ok(new UploadImageResponse("Gambar cover game berhasil diperbarui!", imageUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }
}
