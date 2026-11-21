using be_digitalin.Application.Validation;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Domain.Entities;
using be_digitalin.Services.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 3: migrasi /admin/banners dari Minimal API (BannerEndpoints.cs) ke
/// Controller. Seluruh group admin dipindah utuh — file Endpoints/BannerEndpoints.cs
/// sudah tidak berisi apa pun dan dihapus. Perilaku 1:1 termasuk kasus
/// toggle banner yang tidak ada tetap mengembalikan 200 (bukan 404), sama
/// seperti endpoint lama.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/banners")]
[Tags("Manajemen Banner (Admin)")]
public class AdminBannerController : ControllerBase
{
    private readonly IBannerService _bannerService;

    public AdminBannerController(IBannerService bannerService)
    {
        _bannerService = bannerService;
    }

    // GET /api/v1/admin/banners
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Banner>>> GetAll()
    {
        var banners = await _bannerService.GetAllBannersAsync();
        return Ok(banners);
    }

    // POST /api/v1/admin/banners/upload (multipart/form-data: title + file)
    // file nullable: kalau bagian file tidak dikirim, MVC tidak auto-400
    // melainkan masuk ke guard di bawah — sama seperti Minimal API lama.
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] string title,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("File tidak boleh kosong.");

        // tidak boleh upload lebih dari 2MB (cloudflare mahal bjir)
        if (file.Length > AppConstants.FileLimits.MaxImageBytes)
            return BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

        try
        {
            var banner = await _bannerService.CreateBannerAsync(
                title,
                new ImageFileData(file.FileName, file.ContentType, file.OpenReadStream()),
                cancellationToken);
            return Created($"{AppConstants.Api.Prefix}/admin/banners/{banner.Id}", banner);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    // PUT /api/v1/admin/banners/{id}/toggle — nyala/matikan banner
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        // Legacy mengabaikan hasil ToggleBannerStatusAsync (selalu 200 walau id
        // tidak ada) — dipertahankan agar kontrak tidak berubah.
        await _bannerService.ToggleBannerStatusAsync(id);
        return Ok(new MessageResponse("Status banner berhasil diperbarui."));
    }

    // DELETE /api/v1/admin/banners/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var success = await _bannerService.DeleteBannerAsync(id, cancellationToken);
        if (!success) return NotFound(new MessageResponse("Banner tidak ditemukan."));

        return Ok(new MessageResponse("Banner dan file gambar berhasil dibumihanguskan!"));
    }
}
