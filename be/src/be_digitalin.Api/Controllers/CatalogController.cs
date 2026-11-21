using System.Security.Claims;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Games;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 2: migrasi katalog game publik (GET /games/*) dari Minimal API
/// (group publik di ProductEndpoints.cs) ke Controller. Read-only, tanpa auth,
/// mapping 1:1 — service & kontrak tidak berubah.
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/games")]
[Tags("Katalog Game (Public)")]
public class CatalogController : ControllerBase
{
    /// <summary>
    /// Batas atas ?limit di GET /games/brands. Satu section homepage hanya perlu
    /// satu batch kecil; angka di atas ini hanya menyeret ratusan kartu + ikonnya
    /// sekaligus tanpa ada yang membacanya.
    /// </summary>
    private const int MaxBrandPageSize = 60;

    /// <summary>
    /// Batas jumlah slug guest di ?slugs= GET /games/brands/recent. Cermin
    /// batas cookie FE — slug berlebih dibuang sebelum menyentuh service.
    /// </summary>
    private const int MaxRecentSlugs = 12;

    private readonly IProductService _productService;

    public CatalogController(IProductService productService)
    {
        _productService = productService;
    }

    // GET /api/v1/games/brands?category=&offset=&limit=
    // Tanpa query param = seluruh katalog (kompatibel dengan pemanggil lama).
    [HttpGet("brands")]
    public async Task<ActionResult<IEnumerable<CategoryInfo>>> GetBrands(
        [FromQuery] string? category,
        [FromQuery] int offset = 0,
        [FromQuery] int? limit = null)
    {
        if (offset < 0) offset = 0;
        if (limit < 1) limit = 1;
        if (limit > MaxBrandPageSize) limit = MaxBrandPageSize;

        var brands = await _productService.GetUniqueGameBrandsAsync(category, offset, limit);
        return Ok(brands);
    }

    // GET /api/v1/games/brands/popular
    // Segmen literal "brands/popular" menang atas parameter {brandSlug} di
    // attribute routing (tidak bergantung urutan deklarasi seperti Minimal API).
    [HttpGet("brands/popular")]
    public async Task<ActionResult<IEnumerable<CategoryInfo>>> GetPopularBrands()
    {
        var brands = await _productService.GetPopularGameBrandsAsync();
        return Ok(brands);
    }

    // GET /api/v1/games/brands/recent?slugs=a,b,c
    // Section "Terakhir Dibeli". Optional-auth (pola POST /orders): login →
    // riwayat pembelian DB milik customer (klaim, bukan input klien); guest →
    // slug cookie FE yang divalidasi ulang vs katalog aktif. Slug tidak pernah
    // dijadikan bukti pembelian maupun otorisasi.
    [HttpGet("brands/recent")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<CategoryInfo>>> GetRecentBrands(
        [FromQuery] string? slugs)
    {
        // Batas defensif panjang input — service tetap memvalidasi isi tiap
        // slug; ini hanya mencegah querystring raksasa diproses lebih jauh.
        var guestSlugs = string.IsNullOrWhiteSpace(slugs)
            ? null
            : slugs
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Take(MaxRecentSlugs)
                .ToList();

        int? customerId = null;
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(AppConstants.Roles.Customer))
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedId))
            {
                customerId = parsedId;
            }
        }

        var brands = await _productService.GetRecentGameBrandsAsync(customerId, guestSlugs);
        return Ok(brands);
    }

    // GET /api/v1/games/categories
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<GameCategoryInfo>>> GetCategories()
    {
        var categories = await _productService.GetGameCategoriesAsync();
        return Ok(categories);
    }

    // GET /api/v1/games/{brandSlug}
    [HttpGet("{brandSlug}")]
    public async Task<ActionResult<GameDetailResponse>> GetGameDetail(string brandSlug)
    {
        var game = await _productService.GetGameDetailAsync(brandSlug);

        return game == null
            ? NotFound(new MessageResponse("Game tidak ditemukan"))
            : Ok(game);
    }

    // POST /api/v1/games/{brandSlug}/check-username
    [HttpPost("{brandSlug}/check-username")]
    public async Task<ActionResult<UsernameCheckResponse>> CheckUsername(
        string brandSlug,
        [FromBody] Dictionary<string, string?> targetValues,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productService.CheckUsernameAsync(
                brandSlug, targetValues, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }
}
