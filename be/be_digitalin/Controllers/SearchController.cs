using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Games;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 1 (pilot read-only): migrasi GET /search/games dari
/// Minimal API (SearchEndpoints.cs) ke Controller. 1:1 — service & kontrak
/// tidak berubah; Endpoints/SearchEndpoints.cs masih ada tapi tidak lagi
/// didaftarkan di Program.cs.
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/search")]
[Tags("Search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    // GET /api/v1/search/games?q=keyword&limit=10
    [HttpGet("games")]
    public async Task<ActionResult<IEnumerable<GameSearchResult>>> SearchGames(
        [FromQuery] string? q,
        [FromQuery] int limit = 10)
    {
        var results = await _searchService.SearchGamesAsync(q, limit);
        return Ok(results);
    }
}
