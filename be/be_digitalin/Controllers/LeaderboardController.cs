using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Leaderboard pembeli (publik, read-only) — derived data dari order lunak;
/// tidak ada CRUD: admin tidak pernah mengedit rank/totalSpent.
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/leaderboard")]
[Tags("Leaderboard Pembeli (Public)")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    // GET /api/v1/leaderboard?period=today|week|month (default today, maks 10)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryResponse>>> GetLeaderboard(
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        if (!LeaderboardPeriodParser.TryParse(period, out var parsed))
        {
            return BadRequest(new MessageResponse(
                "Periode tidak valid. Gunakan today, week, atau month."));
        }

        var entries = await _leaderboardService.GetLeaderboardAsync(parsed, cancellationToken);
        return Ok(entries);
    }
}
