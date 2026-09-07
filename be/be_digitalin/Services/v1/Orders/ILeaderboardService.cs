using be_digitalin.DTOs;

namespace be_digitalin.Services.Orders;

public interface ILeaderboardService
{
    /// <summary>
    /// Top pembeli periode tertentu: agregasi Sum(TotalPaid) atas order
    /// berstatus lunak (PAID/PROCESSING/COMPLETED/NEEDS_ACTION) milik customer
    /// teridentifikasi, urut turun + tie-break CustomerId, maks 10.
    /// Semua agregasi terjadi di database (EF Core), rank dihitung dari hasil.
    /// </summary>
    Task<IReadOnlyList<LeaderboardEntryResponse>> GetLeaderboardAsync(
        LeaderboardPeriod period,
        CancellationToken cancellationToken = default);
}
