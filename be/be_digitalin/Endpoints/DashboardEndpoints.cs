using be_digitalin.DTOs;
using Microsoft.AspNetCore.Mvc;
using be_digitalin.Services.Admin;

namespace be_digitalin.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/admin/dashboard")
                            .WithTags("Dashboard Admin")
                            .RequireAuthorization("AdminOnly");

        // GET /admin/dashboard/stats
        adminGroup.MapGet("/stats", async ([FromServices] IDashboardService dashboardService) =>
        {
            var stats = await dashboardService.GetStatsAsync();
            return Results.Ok(stats);
        });

        // GET /admin/dashboard/chart?range={range}[&startDate=YYYY-MM-DD&endDate=YYYY-MM-DD]
        adminGroup.MapGet("/chart", async (
            [FromQuery] string range,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromServices] IDashboardService dashboardService) =>
        {
            var (data, error) = await dashboardService.GetChartAsync(range, startDate, endDate);

            if (error != null)
                return Results.BadRequest(new { message = error });

            return Results.Ok(data);
        });

        // GET /admin/dashboard/balance
        adminGroup.MapGet("/balance", async (
            [FromServices] IDashboardService dashboardService,
            CancellationToken cancellationToken) =>
        {
            var (deposit, error) = await dashboardService.GetSupplierBalanceAsync(cancellationToken);

            // 502: yang gagal adalah supplier di hulu, bukan request admin-nya.
            if (error != null)
                return Results.Json(new { message = error }, statusCode: StatusCodes.Status502BadGateway);

            return Results.Ok(new DigiflazzBalanceResponse { Deposit = deposit!.Value });
        });
    }
}
