namespace be_digitalin.Services.Orders;

/// <summary>Periode leaderboard pembeli.</summary>
public enum LeaderboardPeriod
{
    Today,
    Week,
    Month
}

/// <summary>
/// SSOT parsing nilai `?period=` — dipakai controller (validasi) dan
/// potensial pemanggil service lain, supaya nilai yang dikenali tidak bisa
/// lepas sinkron. String kosong/null = default "today" (ramah tanpa param).
/// </summary>
public static class LeaderboardPeriodParser
{
    public static bool TryParse(string? value, out LeaderboardPeriod period)
    {
        // null / kosong / whitespace = default "today" (ramah tanpa param).
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "today"
            : value.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "today":
                period = LeaderboardPeriod.Today;
                return true;
            case "week":
                period = LeaderboardPeriod.Week;
                return true;
            case "month":
                period = LeaderboardPeriod.Month;
                return true;
            default:
                period = default;
                return false;
        }
    }
}
