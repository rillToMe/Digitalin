using be_digitalin.DTOs;
using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Leaderboard pembeli — ranking Sum(TotalPaid) atas order yang benar-benar
/// dibayar, per periode Today/Week/Month (WIB, konvensi sama dengan
/// DashboardService). Derived data murni: tanpa tabel sendiri, tanpa rank
/// yang disimpan, tanpa cache — satu kueri agregasi EF Core per request.
/// </summary>
public class LeaderboardService : ILeaderboardService
{
    /// <summary>Slot papan peringkat — 10 teratas per periode.</summary>
    public const int MaxEntries = 10;

    private static readonly TimeZoneInfo Wib =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    /// <summary>
    /// Status "uang sudah masuk" — seluruh status pasca-pembayaran dari state
    /// machine OrderStateTransition. PENDING (belum bayar), EXPIRED/FAILED
    /// (bayar gagal), dan REFUNDED (uang kembali) dikecualikan.
    /// </summary>
    private static readonly string[] PaidStatuses =
    [
        AppConstants.OrderStatus.Paid,
        AppConstants.OrderStatus.Processing,
        AppConstants.OrderStatus.Completed,
        AppConstants.OrderStatus.NeedsAction
    ];

    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public LeaderboardService(IApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<LeaderboardEntryResponse>> GetLeaderboardAsync(
        LeaderboardPeriod period,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var startUtc = GetPeriodStartUtc(period, nowUtc);

        // Salin ke variabel lokal: EF Core mem-parameter-kan closure dan
        // menerjemahkannya ke IN(...) — akses field static langsung di pohon
        // ekspresi justru gagal dievaluasi. List dipakai agar binding
        // Contains pasti ke Enumerable (bukan MemoryExtensions/ReadOnlySpan).
        var paidStatuses = PaidStatuses.ToList();

        // Filter → join profil → group per customer → Sum → sort → Take(10):
        // semuanya diterjemahkan EF Core ke SQL (WHERE/JOIN/GROUP BY/SUM/
        // ORDER BY/LIMIT); C# hanya menerima maks 10 baris final.
        // Tie-break CustomerId ascending: total sama → urutan selalu deterministik.
        var entries = await _db.TopupOrders
            .AsNoTracking()
            .Where(o => o.CustomerId != null
                && paidStatuses.Contains(o.Status)
                && o.OrderDate >= startUtc
                && o.OrderDate < nowUtc)
            .Join(_db.Customers.AsNoTracking(),
                o => o.CustomerId,
                c => c.Id,
                (o, c) => new { o.CustomerId, c.FullName, c.AvatarUrl, o.TotalPaid })
            .GroupBy(x => new { x.CustomerId, x.FullName, x.AvatarUrl })
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.FullName,
                g.Key.AvatarUrl,
                TotalSpent = g.Sum(x => x.TotalPaid)
            })
            .OrderByDescending(x => x.TotalSpent)
            .ThenBy(x => x.CustomerId)
            .Take(MaxEntries)
            .ToListAsync(cancellationToken);

        // Rank dihitung dari urutan hasil kueri — tidak disimpan di DB.
        return entries
            .Select((x, i) => new LeaderboardEntryResponse(
                i + 1,
                MaskDisplayName(x.FullName),
                x.AvatarUrl,
                x.TotalSpent))
            .ToList();
    }

    /// <summary>
    /// Awal periode dalam UTC (setengah-terbuka: start &lt;= OrderDate &lt; now).
    /// Semua batas dihitung di kalender WIB — sama dengan konvensi dashboard.
    /// Minggu = Senin 00:00 WIB (belum ada konvensi minggu lain di project);
    /// bulan = tanggal 1 00:00 WIB.
    /// </summary>
    private static DateTime GetPeriodStartUtc(LeaderboardPeriod period, DateTime nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Wib);

        var localStart = period switch
        {
            LeaderboardPeriod.Today => localNow.Date,
            LeaderboardPeriod.Week => localNow.Date.AddDays(
                -((int)localNow.DayOfWeek + 6) % 7), // mundur ke Senin
            LeaderboardPeriod.Month => new DateTime(
                localNow.Year, localNow.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };

        return TimeZoneInfo.ConvertTimeToUtc(localStart, Wib);
    }

    /// <summary>
    /// FullName bisa email turunan (fallback auto-register OAuth/Google bila
    /// provider tidak mengirim nama). Email = PII → samarkan bentuk email
    /// menjadi prefiks pendek (maks 3 karakter): "budi.santoso@gmail.com" →
    /// "bud***". Nama biasa ditampilkan apa adanya.
    /// </summary>
    private static string MaskDisplayName(string fullName)
    {
        if (!fullName.Contains('@'))
            return fullName;

        var localPart = fullName.Split('@')[0];
        var prefix = localPart.Length <= 3 ? localPart : localPart[..3];
        return $"{prefix}***";
    }
}
