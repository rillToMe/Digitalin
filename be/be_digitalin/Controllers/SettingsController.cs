using be_digitalin.Constants;
using be_digitalin.DTOs.Requests;
using be_digitalin.DTOs.Responses;
using be_digitalin.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 1 (pilot): migrasi /admin/settings/maintenance dari Minimal API
/// (SettingsEndpoints.cs) ke Controller. GET read-only + PUT ikut dipindah
/// supaya MapGroup lama bisa dilepas utuh (route tidak boleh dobel).
/// PUT memakai auto-validation [ApiController] — bentuk error 400 dipaksa
/// { message } oleh shim InvalidModelStateResponseFactory (Fase 0), identik
/// dengan Validator.TryValidateObject manual di endpoint lama.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/settings")]
[Tags("Settings Admin")]
public class SettingsController : ControllerBase
{
    private readonly IAppSettingsService _appSettings;

    public SettingsController(IAppSettingsService appSettings)
    {
        _appSettings = appSettings;
    }

    // GET /api/v1/admin/settings/maintenance
    [HttpGet("maintenance")]
    public async Task<ActionResult<MaintenanceStatusResponse>> GetMaintenance()
    {
        var config = await _appSettings.GetMaintenanceConfigAsync();

        return Ok(ToResponse(config));
    }

    // PUT /api/v1/admin/settings/maintenance
    // [ApiController] auto-validate DataAnnotations pada UpdateMaintenanceRequest
    // → InvalidModelStateResponseFactory mengembalikan { message } (kontrak lama).
    [HttpPut("maintenance")]
    public async Task<ActionResult<MaintenanceStatusResponse>> UpdateMaintenance(
        [FromBody] UpdateMaintenanceRequest req)
    {
        var config = new MaintenanceConfig(
            Enabled:         req.Enabled,
            Title:           req.Title,
            Message:         req.Message,
            EstimatedFinish: string.IsNullOrWhiteSpace(req.EstimatedFinish) ? null : req.EstimatedFinish,
            AllowAdmin:      req.AllowAdmin
        );

        await _appSettings.UpdateMaintenanceConfigAsync(config);

        return Ok(ToResponse(config));
    }

    private static MaintenanceStatusResponse ToResponse(MaintenanceConfig config) =>
        new(
            Enabled:         config.Enabled,
            Title:           config.Title,
            Message:         config.Message,
            EstimatedFinish: config.EstimatedFinish,
            AllowAdmin:      config.AllowAdmin
        );
}
