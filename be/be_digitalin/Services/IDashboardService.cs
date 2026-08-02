using be_digitalin.DTOs;

namespace be_digitalin.Services;

public interface IDashboardService
{
    Task<DashboardStatsResponse> GetStatsAsync();

    Task<(IEnumerable<ChartDataResponse>? Data, string? Error)> GetChartAsync(
        string range, string? startDate, string? endDate);

    Task<(decimal? Deposit, string? Error)> GetSupplierBalanceAsync(
        CancellationToken cancellationToken = default);
}
