namespace be_digitalin.DTOs;

public class DashboardStatsResponse
{
    public decimal TotalRevenueToday { get; set; }
    public decimal TotalProfitToday { get; set; }

    public long TotalOrdersToday { get; set; }
    public long SuccessOrdersToday { get; set; }
    public long PendingOrdersToday { get; set; }
    public long FailedOrdersToday { get; set; }
}

public record ChartDataResponse(
    string Name,
    decimal Pendapatan,
    decimal Profit
);

// Saldo supplier — diambil live dari Digiflazz, tidak disimpan di database.
public class DigiflazzBalanceResponse
{
    public decimal Deposit { get; set; }
}