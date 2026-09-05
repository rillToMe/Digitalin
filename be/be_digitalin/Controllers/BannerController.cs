using be_digitalin.Constants;
using be_digitalin.Models;
using be_digitalin.Services.Content;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 2: migrasi banner publik (GET /banners) dari Minimal API
/// (group publik di BannerEndpoints.cs) ke Controller. Read-only, tanpa auth.
/// Group admin (/admin/banners) tetap Minimal API sampai Fase 3/4.
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/banners")]
[Tags("Banner Promosi (Public)")]
public class BannerController : ControllerBase
{
    private readonly IBannerService _bannerService;

    public BannerController(IBannerService bannerService)
    {
        _bannerService = bannerService;
    }

    // GET /api/v1/banners
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Banner>>> GetActiveBanners()
    {
        var banners = await _bannerService.GetActiveBannersAsync();
        return Ok(banners);
    }
}
