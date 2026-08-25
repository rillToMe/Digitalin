using System.ComponentModel.DataAnnotations;
using be_digitalin.DTOs.Requests;
using be_digitalin.DTOs.Responses;
using Microsoft.AspNetCore.Mvc;
using be_digitalin.Services.Settings;

namespace be_digitalin.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settingsGroup = app.MapGroup("/admin/settings")
            .WithTags("Settings Admin")
            .RequireAuthorization("AdminOnly");

        // ── GET /admin/settings/maintenance ───────────────────────────────────
        settingsGroup.MapGet("/maintenance", async (
            [FromServices] IAppSettingsService appSettings) =>
        {
            var config = await appSettings.GetMaintenanceConfigAsync();

            return Results.Ok(new MaintenanceStatusResponse(
                Enabled:         config.Enabled,
                Title:           config.Title,
                Message:         config.Message,
                EstimatedFinish: config.EstimatedFinish,
                AllowAdmin:      config.AllowAdmin
            ));
        });

        // ── PUT /admin/settings/maintenance ───────────────────────────────────
        settingsGroup.MapPut("/maintenance", async (
            [FromBody] UpdateMaintenanceRequest req,
            [FromServices] IAppSettingsService appSettings) =>
        {
            // Manual validation (Minimal API does not auto-validate DataAnnotations)
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(req, new ValidationContext(req), validationResults, validateAllProperties: true))
            {
                return Results.BadRequest(new { message = validationResults[0].ErrorMessage });
            }

            var config = new MaintenanceConfig(
                Enabled:         req.Enabled,
                Title:           req.Title,
                Message:         req.Message,
                EstimatedFinish: string.IsNullOrWhiteSpace(req.EstimatedFinish) ? null : req.EstimatedFinish,
                AllowAdmin:      req.AllowAdmin
            );

            await appSettings.UpdateMaintenanceConfigAsync(config);

            return Results.Ok(new MaintenanceStatusResponse(
                Enabled:         config.Enabled,
                Title:           config.Title,
                Message:         config.Message,
                EstimatedFinish: config.EstimatedFinish,
                AllowAdmin:      config.AllowAdmin
            ));
        });
    }
}
