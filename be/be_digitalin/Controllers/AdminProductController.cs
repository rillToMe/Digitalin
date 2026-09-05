using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Services.Games;
using be_digitalin.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 3: migrasi sebagian /admin/products dari Minimal API
/// (ProductEndpoints.cs) — GET list (baca) dan PUT margin (aksi sederhana).
/// POST create (bind GameProduct mentah) & POST upload-item-image menyusul di
/// Fase 4 bersama AdminSystemController. Tidak ada route dobel (metode beda).
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/products")]
[Tags("Manajemen Produk (Admin)")]
public class AdminProductController : ControllerBase
{
    private readonly IProductService _productService;

    public AdminProductController(IProductService productService)
    {
        _productService = productService;
    }

    // GET /api/v1/admin/products — pencarian & paginasi
    [HttpGet]
    public async Task<ActionResult<PagedResult<GameProduct>>> List(
        [FromQuery] string? search,
        [FromQuery] string? brand,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        if (limit > 100) limit = 100;
        if (page < 1) page = 1;

        var result = await _productService.GetPagedProductsAsync(search, brand, page, limit);
        return Ok(result);
    }

    // PUT /api/v1/admin/products/{id}/margin — set margin khusus per item
    [HttpPut("{id}/margin")]
    public async Task<IActionResult> UpdateMargin(int id, [FromBody] UpdateCustomMarginRequest req)
    {
        var updated = await _productService.UpdateProductMarginAsync(id, req.Margin);
        return updated
            ? Ok(new MessageResponse("Margin berhasil diatur!"))
            : NotFound();
    }

    // POST /api/v1/admin/products — buat produk manual
    // CATATAN: bind entity GameProduct mentah dipertahankan dari endpoint lama
    // (parity kontrak; risiko over-posting dicatat di migration.md §5, bukan
    // diubah diam-diam di fase ini).
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GameProduct product)
    {
        try
        {
            var createdProduct = await _productService.CreateProductAsync(product);
            return Created($"{AppConstants.Api.Prefix}/admin/products/{createdProduct.Id}", createdProduct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // POST /api/v1/admin/products/{id}/upload-item-image — upload ikon (R2)
    [HttpPost("{id}/upload-item-image")]
    // file nullable: kalau bagian file tidak dikirim, MVC tidak auto-400
    // ("The file field is required.") melainkan masuk ke guard di bawah —
    // sama seperti perilaku Minimal API lama.
    public async Task<IActionResult> UploadItemImage(
        int id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("File tidak boleh kosong.");

        // tolak file yang lebih dari 2MB
        if (file.Length > AppConstants.FileLimits.MaxImageBytes)
            return BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

        try
        {
            var imageUrl = await _productService.UpdateProductIconAsync(id, file, cancellationToken);
            if (imageUrl == null) return NotFound(new MessageResponse("Produk tidak ditemukan."));

            return Ok(new UploadImageResponse("Gambar item nominal berhasil diperbarui!", imageUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }
}
