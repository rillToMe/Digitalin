namespace be_digitalin.DTOs;

public record DashboardStatsResponse(
    decimal TotalRevenueToday,
    decimal TotalProfitToday,
    int TotalOrdersToday,
    int SuccessOrdersToday,
    int PendingOrdersToday,
    int FailedOrdersToday
);

public record ChartDataResponse(
    string Name, 
    decimal Pendapatan,
    decimal Profit
);