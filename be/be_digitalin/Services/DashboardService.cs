using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Constants;
using be_digitalin.Providers;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services;

public class DashboardService : IDashboardService
{
    private readonly DigitalinDbContext _db;
    private readonly DigiflazzProvider _digiflazz;
    private static readonly TimeZoneInfo Wib = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    public DashboardService(DigitalinDbContext db, DigiflazzProvider digiflazz)
    {
        _db = db;
        _digiflazz = digiflazz;
    }

    // ── GET /admin/dashboard/stats ──────────────────────────────

    public async Task<DashboardStatsResponse> GetStatsAsync()
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Wib);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, Wib);

        // Baca langsung dari TopupOrder — tanpa join Product
        var todayOrders = await _db.TopupOrders
            .AsNoTracking()
            .Where(o => o.OrderDate >= todayStartUtc)
            .ToListAsync();

        var completedOrders = todayOrders
            .Where(o => o.Status == AppConstants.OrderStatus.Completed)
            .ToList();

        return new DashboardStatsResponse
        {
            TotalRevenueToday = completedOrders.Sum(o => o.TotalPaid),
            TotalProfitToday = completedOrders.Sum(o => o.ProfitAmount),
            TotalOrdersToday = todayOrders.Count,
            SuccessOrdersToday = completedOrders.Count,
            PendingOrdersToday = todayOrders.Count(o => o.Status == AppConstants.OrderStatus.Pending),
            FailedOrdersToday = todayOrders.Count(o =>
                o.Status == AppConstants.OrderStatus.Failed ||
                o.Status == AppConstants.OrderStatus.Expired)
        };
    }

    // ── GET /admin/dashboard/chart ──────────────────────────────

    public async Task<(IEnumerable<ChartDataResponse>? Data, string? Error)> GetChartAsync(
        string range, string? startDate, string? endDate)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Wib);

        DateTime startUtc;
        DateTime endUtc = DateTime.UtcNow;
        string groupBy; // "hour" or "day"

        switch (range?.ToLower())
        {
            case "today":
                startUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, Wib);
                groupBy = "hour";
                break;

            case "yesterday":
                var yesterdayLocal = localNow.Date.AddDays(-1);
                startUtc = TimeZoneInfo.ConvertTimeToUtc(yesterdayLocal, Wib);
                endUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, Wib);
                groupBy = "hour";
                break;

            case "7days":
                startUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(-6), Wib);
                groupBy = "day";
                break;

            case "30days":
                startUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(-29), Wib);
                groupBy = "day";
                break;

            case "custom":
                if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate)
                    || !DateTime.TryParse(startDate, out var parsedStart)
                    || !DateTime.TryParse(endDate, out var parsedEnd))
                {
                    return (null, "Parameter startDate dan endDate wajib diisi dengan format YYYY-MM-DD.");
                }
                if (parsedEnd < parsedStart)
                {
                    return (null, "endDate tidak boleh lebih awal dari startDate.");
                }
                if ((parsedEnd.Date - parsedStart.Date).TotalDays > 31)
                {
                    return (null, "Rentang tanggal maksimal adalah 31 hari.");
                }
                startUtc = TimeZoneInfo.ConvertTimeToUtc(parsedStart.Date, Wib);
                endUtc = TimeZoneInfo.ConvertTimeToUtc(parsedEnd.Date.AddDays(1), Wib);
                groupBy = "day";
                break;

            default:
                return (null, "Parameter range tidak valid. Pilih: today, yesterday, 7days, 30days, atau custom.");
        }

        // Query dengan EF Core — langsung dari snapshot TopupOrder
        var completedOrders = await _db.TopupOrders
            .AsNoTracking()
            .Where(o => o.OrderDate >= startUtc && o.OrderDate < endUtc
                     && o.Status == AppConstants.OrderStatus.Completed)
            .Select(o => new
            {
                o.OrderDate,
                o.TotalPaid,
                o.ProfitAmount
            })
            .ToListAsync();

        // Convert UTC → WIB and group in memory (EF Core can't translate AddHours)
        var wibOrders = completedOrders.Select(o => new
        {
            WibDate = o.OrderDate.AddHours(7),
            o.TotalPaid,
            o.ProfitAmount
        }).ToList();

        var result = groupBy == "hour"
            ? wibOrders
                .GroupBy(o => o.WibDate.Hour)
                .OrderBy(g => g.Key)
                .Select(g => new ChartDataResponse(
                    $"{g.Key:D2}:00",
                    g.Sum(o => o.TotalPaid),
                    g.Sum(o => o.ProfitAmount)))
                .ToList()
            : wibOrders
                .GroupBy(o => o.WibDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ChartDataResponse(
                    g.Key.ToString("dd/MM"),
                    g.Sum(o => o.TotalPaid),
                    g.Sum(o => o.ProfitAmount)))
                .ToList();

        return (result, null);
    }

    // ── GET /admin/dashboard/balance ────────────────────────────

    public async Task<(decimal? Deposit, string? Error)> GetSupplierBalanceAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return (await _digiflazz.GetBalanceAsync(cancellationToken), null);
        }
        catch (Exception ex)
        {
            // Saldo diambil live dari pihak ketiga: kalau Digiflazz down, timeout,
            // atau menolak signature, kartu saldo saja yang gagal — sisa dashboard
            // tetap harus tampil, jadi error-nya tidak boleh dilempar jadi 500.
            Console.WriteLine($"[DASHBOARD BALANCE ERROR] {ex.Message}");
            return (null, $"Gagal mengambil saldo dari Digiflazz: {ex.Message}");
        }
    }
}
