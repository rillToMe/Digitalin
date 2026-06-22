using be_digitalin.Services;
using Microsoft.AspNetCore.Mvc;

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
    }
}
