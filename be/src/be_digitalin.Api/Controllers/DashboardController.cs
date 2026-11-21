using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 1 (pilot read-only): migrasi GET /admin/dashboard/* dari
/// Minimal API (DashboardEndpoints.cs) ke Controller. Semua action read-only,
/// otorisasi di level class (sama seperti RequireAuthorization("AdminOnly")
/// pada MapGroup lama).
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/dashboard")]
[Tags("Dashboard Admin")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    // GET /api/v1/admin/dashboard/stats
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsResponse>> GetStats()
    {
        var stats = await _dashboardService.GetStatsAsync();
        return Ok(stats);
    }

    // GET /api/v1/admin/dashboard/chart?range={range}[&startDate=YYYY-MM-DD&endDate=YYYY-MM-DD]
    // Parameter dibuat nullable agar validasi range tetap ditangani service
    // (return error → 400 { message }) — persis perilaku endpoint Minimal API lama.
    [HttpGet("chart")]
    public async Task<IActionResult> GetChart(
        [FromQuery] string? range,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate)
    {
        var (data, error) = await _dashboardService.GetChartAsync(range!, startDate, endDate);

        if (error != null)
            return BadRequest(new MessageResponse(error));

        return Ok(data);
    }

    // GET /api/v1/admin/dashboard/balance
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken cancellationToken)
    {
        var (deposit, error) = await _dashboardService.GetSupplierBalanceAsync(cancellationToken);

        // 502: yang gagal adalah supplier di hulu, bukan request admin-nya.
        if (error != null)
            return StatusCode(StatusCodes.Status502BadGateway, new MessageResponse(error));

        return Ok(new DigiflazzBalanceResponse { Deposit = deposit!.Value });
    }
}
