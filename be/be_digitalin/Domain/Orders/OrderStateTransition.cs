using be_digitalin.Constants;

namespace be_digitalin.Domain.Orders;

/// <summary>
/// Single Source of Truth untuk aturan transisi status pesanan (order state machine).
///
/// Semua komponen yang mengubah <c>TopupOrder.Status</c> — webhook Midtrans,
/// webhook Digiflazz, bot patroli (OrderSweepWorker), retry, refund — mengacu ke
/// aturan di sini. Jika nanti ada provider baru, cukup satu tempat yang diubah.
///
/// Diagram alur normal:
///
///   PENDING ──► PAID ──► PROCESSING ──► COMPLETED
///      │          │           │
///      │          │           └──► NEEDS_ACTION ──► COMPLETED / PROCESSING / REFUNDED
///      │          └──► NEEDS_ACTION
///      ├──► EXPIRED
///      └──► FAILED
///
/// COMPLETED, EXPIRED, FAILED, REFUNDED adalah status final (tidak punya transisi keluar).
/// Contoh yang ditolak: COMPLETED ──► NEEDS_ACTION.
/// </summary>
public static class OrderStateTransition
{
    // Peta transisi yang diizinkan: from → himpunan to yang sah.
    private static readonly IReadOnlyDictionary<string, HashSet<string>> Allowed =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [AppConstants.OrderStatus.Pending] = new(StringComparer.Ordinal)
            {
                AppConstants.OrderStatus.Paid,
                AppConstants.OrderStatus.Expired,
                AppConstants.OrderStatus.Failed,
            },
            [AppConstants.OrderStatus.Paid] = new(StringComparer.Ordinal)
            {
                AppConstants.OrderStatus.Processing,
                AppConstants.OrderStatus.Completed,
                AppConstants.OrderStatus.NeedsAction,
            },
            [AppConstants.OrderStatus.Processing] = new(StringComparer.Ordinal)
            {
                AppConstants.OrderStatus.Completed,
                AppConstants.OrderStatus.NeedsAction,
            },
            [AppConstants.OrderStatus.NeedsAction] = new(StringComparer.Ordinal)
            {
                AppConstants.OrderStatus.Processing,
                AppConstants.OrderStatus.Completed,
                AppConstants.OrderStatus.Refunded,
            },

            // Status final — tidak ada transisi keluar.
            [AppConstants.OrderStatus.Completed] = new(StringComparer.Ordinal),
            [AppConstants.OrderStatus.Expired]   = new(StringComparer.Ordinal),
            [AppConstants.OrderStatus.Failed]    = new(StringComparer.Ordinal),
            [AppConstants.OrderStatus.Refunded]  = new(StringComparer.Ordinal),
        };

    /// <summary>
    /// True bila transisi <paramref name="from"/> → <paramref name="to"/> diizinkan.
    /// Transisi ke status yang sama (idempoten) dianggap valid.
    /// </summary>
    public static bool CanTransition(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
            return true;

        return Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
    }
}
