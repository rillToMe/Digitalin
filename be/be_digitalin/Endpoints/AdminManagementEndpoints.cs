using System.Security.Claims;
using be_digitalin.DTOs;
using Microsoft.AspNetCore.Mvc;
using be_digitalin.Services.Admin;

namespace be_digitalin.Endpoints;

public static class AdminManagementEndpoints
{
    public static void MapAdminManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/admin/admins")
            .WithTags("Admin Management")
            .RequireAuthorization("SuperAdminOnly");

        // ── GET /admin/admins ─────────────────────────────────────────────────
        adminGroup.MapGet("/", async ([FromServices] IAdminManagementService service) =>
        {
            var admins = await service.GetAdminsAsync();
            return Results.Ok(admins);
        });

        // ── POST /admin/admins ────────────────────────────────────────────────
        adminGroup.MapPost("/", async (
            [FromBody] CreateAdminRequest req,
            [FromServices] IAdminManagementService service) =>
        {
            var result = await service.CreateAdminAsync(req);
            return MapAdminActionResult(result);
        });

        // ── PUT /admin/admins/{id}/role ───────────────────────────────────────
        adminGroup.MapPut("/{id:int}/role", async (
            int id,
            [FromBody] UpdateAdminRoleRequest req,
            ClaimsPrincipal user,
            [FromServices] IAdminManagementService service) =>
        {
            var callerUsername = user.FindFirst(ClaimTypes.Name)?.Value;
            var result = await service.UpdateAdminRoleAsync(id, req.Role, callerUsername);
            return MapAdminActionResult(result);
        });

        // ── PUT /admin/admins/{id}/password ───────────────────────────────────
        adminGroup.MapPut("/{id:int}/password", async (
            int id,
            [FromBody] ResetAdminPasswordRequest req,
            [FromServices] IAdminManagementService service) =>
        {
            var result = await service.ResetAdminPasswordAsync(id, req.NewPassword);
            return MapAdminActionResult(result);
        });

        // ── DELETE /admin/admins/{id} ─────────────────────────────────────────
        adminGroup.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            [FromServices] IAdminManagementService service) =>
        {
            var callerUsername = user.FindFirst(ClaimTypes.Name)?.Value;
            var result = await service.DeleteAdminAsync(id, callerUsername);
            return MapAdminActionResult(result);
        });
    }

    // ── Helpers ─────────────────────────────────────────────────
    private static IResult MapAdminActionResult(AdminActionResult result) =>
        result.Outcome switch
        {
            AdminActionOutcome.Success    => Results.Ok(new { message = result.Message }),
            AdminActionOutcome.NotFound   => Results.NotFound(new { message = result.Message }),
            _                             => Results.BadRequest(new { message = result.Message }),
        };
}
