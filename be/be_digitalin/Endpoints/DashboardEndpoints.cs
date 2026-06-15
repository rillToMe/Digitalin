using System.Data;
using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Constants;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/admin/dashboard")
                            .WithTags("Dashboard Admin (High Performance Dapper)")
                            .RequireAuthorization("AdminOnly"); // Diproteksi JWT + wajib role ADMIN

        // 1. GET /admin/dashboard/stats
        adminGroup.MapGet("/stats", async ([FromServices] DigitalinDbContext db) =>
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var wibTodayStart = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, tz);

            using var conn = db.Database.GetDbConnection();

            // FIX BUG 1: Tambahkan ::int di akhir COUNT() agar COCOK dengan tipe data 'int' di C# DTO
            string sql = $@"
                SELECT 
                    COALESCE(SUM(CASE WHEN o.""Status"" = '{AppConstants.OrderStatus.Completed}' THEN p.""Price"" ELSE 0 END), 0) AS TotalRevenueToday,
                    COALESCE(SUM(CASE WHEN o.""Status"" = '{AppConstants.OrderStatus.Completed}' THEN COALESCE(p.""CustomMargin"", 0) ELSE 0 END), 0) AS TotalProfitToday,
                    COUNT(o.""Id"")::int AS TotalOrdersToday,
                    COUNT(CASE WHEN o.""Status"" = '{AppConstants.OrderStatus.Completed}' THEN 1 END)::int AS SuccessOrdersToday,
                    COUNT(CASE WHEN o.""Status"" = '{AppConstants.OrderStatus.Pending}' THEN 1 END)::int AS PendingOrdersToday,
                    COUNT(CASE WHEN o.""Status"" IN ('{AppConstants.OrderStatus.Failed}', '{AppConstants.OrderStatus.Expired}') THEN 1 END)::int AS FailedOrdersToday
                FROM ""TopupOrders"" o
                LEFT JOIN ""GameProducts"" p ON o.""GameProductId"" = p.""Id""
                WHERE o.""OrderDate"" >= @TodayStart;";

            var stats = await conn.QuerySingleAsync<DashboardStatsResponse>(sql, new { TodayStart = wibTodayStart });
            return Results.Ok(stats);
        });

        // 2. GET /admin/dashboard/chart?range={range}[&startDate=YYYY-MM-DD&endDate=YYYY-MM-DD]
        // [M12] A "custom" range is now supported with startDate/endDate query params.
        // The maximum allowed span is 31 days to prevent expensive full-table scans.
        adminGroup.MapGet("/chart", async (
            [FromQuery] string range,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromServices] DigitalinDbContext db) =>
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            
            DateTime startUtc;
            DateTime endUtc = DateTime.UtcNow;
            string sql;

            using var conn = db.Database.GetDbConnection();

            switch (range?.ToLower())
            {
                case "today":
                    startUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, tz);
                    sql = $@"
                        SELECT 
                            TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'HH24:00') AS Name,
                            SUM(p.""Price"") AS Pendapatan,
                            SUM(COALESCE(p.""CustomMargin"", 0)) AS Profit
                        FROM ""TopupOrders"" o
                        LEFT JOIN ""GameProducts"" p ON o.""GameProductId"" = p.""Id""
                        WHERE o.""OrderDate"" >= @Start AND o.""Status"" = '{AppConstants.OrderStatus.Completed}'
                        GROUP BY TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'HH24:00')
                        ORDER BY Name;";
                    break;

                case "yesterday":
                    var yesterdayLocal = localNow.Date.AddDays(-1);
                    startUtc = TimeZoneInfo.ConvertTimeToUtc(yesterdayLocal, tz);
                    endUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, tz);
                    sql = $@"
                        SELECT 
                            TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'HH24:00') AS Name,
                            SUM(p.""Price"") AS Pendapatan,
                            SUM(COALESCE(p.""CustomMargin"", 0)) AS Profit
                        FROM ""TopupOrders"" o
                        LEFT JOIN ""GameProducts"" p ON o.""GameProductId"" = p.""Id""
                        WHERE o.""OrderDate"" >= @Start AND o.""OrderDate"" < @End AND o.""Status"" = '{AppConstants.OrderStatus.Completed}'
                        GROUP BY TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'HH24:00')
                        ORDER BY Name;";
                    break;

                case "7days":
                    startUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(-6), tz);
                    sql = $@"
                        SELECT 
                            TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'DD Mon') AS Name,
                            SUM(p.""Price"") AS Pendapatan,
                            SUM(COALESCE(p.""CustomMargin"", 0)) AS Profit
                        FROM ""TopupOrders"" o
                        LEFT JOIN ""GameProducts"" p ON o.""GameProductId"" = p.""Id""
                        WHERE o.""OrderDate"" >= @Start AND o.""Status"" = '{AppConstants.OrderStatus.Completed}'
                        GROUP BY DATE(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta'), TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'DD Mon')
                        ORDER BY DATE(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta');";
                    break;

                case "30days":
                    startUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(-29), tz);
                    sql = $@"
                        SELECT 
                            TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'DD/MM') AS Name,
                            SUM(p.""Price"") AS Pendapatan,
                            SUM(COALESCE(p.""CustomMargin"", 0)) AS Profit
                        FROM ""TopupOrders"" o
                        LEFT JOIN ""GameProducts"" p ON o.""GameProductId"" = p.""Id""
                        WHERE o.""OrderDate"" >= @Start AND o.""Status"" = '{AppConstants.OrderStatus.Completed}'
                        GROUP BY DATE(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta'), TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'DD/MM')
                        ORDER BY DATE(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta');";
                    break;

                // ── [M12] Custom date range with 31-day maximum span ─────────
                case "custom":
                    if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate)
                        || !DateTime.TryParse(startDate, out var parsedStart)
                        || !DateTime.TryParse(endDate, out var parsedEnd))
                    {
                        return Results.BadRequest(new { message = "Parameter startDate dan endDate wajib diisi dengan format YYYY-MM-DD." });
                    }

                    if (parsedEnd < parsedStart)
                    {
                        return Results.BadRequest(new { message = "endDate tidak boleh lebih awal dari startDate." });
                    }

                    // Cap at 31 days to prevent expensive full-table scans
                    if ((parsedEnd.Date - parsedStart.Date).TotalDays > 31)
                    {
                        return Results.BadRequest(new { message = "Rentang tanggal maksimal adalah 31 hari." });
                    }

                    startUtc = TimeZoneInfo.ConvertTimeToUtc(parsedStart.Date, tz);
                    endUtc = TimeZoneInfo.ConvertTimeToUtc(parsedEnd.Date.AddDays(1), tz); // include the end date
                    sql = $@"
                        SELECT 
                            TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'DD/MM') AS Name,
                            SUM(p.""Price"") AS Pendapatan,
                            SUM(COALESCE(p.""CustomMargin"", 0)) AS Profit
                        FROM ""TopupOrders"" o
                        LEFT JOIN ""GameProducts"" p ON o.""GameProductId"" = p.""Id""
                        WHERE o.""OrderDate"" >= @Start AND o.""OrderDate"" < @End AND o.""Status"" = '{AppConstants.OrderStatus.Completed}'
                        GROUP BY DATE(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta'), TO_CHAR(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta', 'DD/MM')
                        ORDER BY DATE(o.""OrderDate"" AT TIME ZONE 'Asia/Jakarta');";
                    break;

                default:
                    return Results.BadRequest(new { message = "Parameter range tidak valid. Pilih: today, yesterday, 7days, 30days, atau custom." });
            }

            var chartData = await conn.QueryAsync<ChartDataResponse>(sql, new { Start = startUtc, End = endUtc });
            return Results.Ok(chartData);
        });
    }
}