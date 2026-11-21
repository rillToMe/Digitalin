using be_digitalin.Constants;
using be_digitalin.Domain.Entities;

namespace be_digitalin.Domain.Orders;

/// <summary>
/// Klasifikasi status pengajuan fulfillment ke supplier (Digiflazz) untuk pesanan
/// yang berada pada status <c>NEEDS_ACTION</c>.
/// </summary>
public enum SupplierFulfillmentState
{
    /// <summary>
    /// Pesanan belum pernah dikirim ke Digiflazz (mis. late payment yang baru
    /// dieskalasi dari EXPIRED/FAILED ke NEEDS_ACTION). Aman untuk di-refund
    /// atau diajukan sekali melalui retry terotorisasi.
    /// </summary>
    NeverSubmitted = 0,

    /// <summary>
    /// Pengajuan ke Digiflazz pernah dilakukan (atau sedang berlangsung) tetapi
    /// hasilnya belum pasti (timeout, gangguan koneksi, HTTP 5xx/429, JSON rusak,
    /// field <c>data</c>/<c>status</c> hilang, atau masih <c>Pending</c>).
    /// Refund dan pengiriman ulang <c>TopupAsync</c> wajib ditahan sampai status
    /// direkonsiliasi secara pasti.
    /// </summary>
    UncertainOutcome = 1,

    /// <summary>
    /// Digiflazz secara eksplisit mengonfirmasi status terminal <c>Gagal</c>.
    /// Aman untuk di-refund; pengiriman ulang dengan <c>ref_id</c> yang sama ditolak.
    /// </summary>
    ConfirmedFailure = 2
}

/// <summary>
/// Aturan domain untuk melacak kepemilikan reservasi kuota voucher dan status
/// pengajuan supplier pada <see cref="TopupOrder"/> tanpa mengubah skema migrasi.
/// </summary>
public static class OrderFulfillmentState
{
    private const int NeverSubmittedMillisecondTag = 101;
    private const int UncertainOutcomeMillisecondTag = 202;
    private const int ConfirmedFailureMillisecondTag = 303;
    private const int StandardProcessedMillisecondTag = 404;

    /// <summary>
    /// True apabila pesanan saat ini memegang reservasi aktif pada kuota <c>Voucher.UsedCount</c>.
    /// </summary>
    public static bool HasActiveVoucherReservation(TopupOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (string.IsNullOrWhiteSpace(order.VoucherCode))
            return false;

        if (string.Equals(order.Status, AppConstants.OrderStatus.Pending, StringComparison.Ordinal))
            return true;

        if (string.Equals(order.Status, AppConstants.OrderStatus.Expired, StringComparison.Ordinal) ||
            string.Equals(order.Status, AppConstants.OrderStatus.Failed, StringComparison.Ordinal) ||
            string.Equals(order.Status, AppConstants.OrderStatus.Refunded, StringComparison.Ordinal))
        {
            return false;
        }

        // Pada status PAID, PROCESSING, NEEDS_ACTION, dan COMPLETED:
        // ProcessedAt != null membuktikan bahwa reservasi voucher aktif dimiliki oleh order ini.
        // ProcessedAt == null pada NEEDS_ACTION hanya terjadi ketika late payment gagal
        // melakukan re-reservasi kuota voucher yang sudah dilepas saat EXPIRED/FAILED.
        return order.ProcessedAt.HasValue;
    }

    /// <summary>
    /// True apabila pesanan memakai kode voucher tetapi tidak lagi memegang reservasi
    /// kuota aktif (mis. late payment masuk setelah kuota voucher habis dipakai order lain).
    /// </summary>
    public static bool HasUnfulfilledVoucherRequirement(TopupOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return !string.IsNullOrWhiteSpace(order.VoucherCode) && !HasActiveVoucherReservation(order);
    }

    /// <summary>
    /// Menentukan status pengajuan supplier untuk pesanan berstatus <c>NEEDS_ACTION</c>.
    /// Fail-closed: setiap nilai <c>ProcessedAt</c> tanpa penanda eksplisit <c>NeverSubmitted</c>
    /// atau <c>ConfirmedFailure</c> diperlakukan sebagai <see cref="SupplierFulfillmentState.UncertainOutcome"/>.
    /// </summary>
    public static SupplierFulfillmentState GetSupplierState(TopupOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!order.ProcessedAt.HasValue)
            return SupplierFulfillmentState.NeverSubmitted;

        return order.ProcessedAt.Value.Millisecond switch
        {
            NeverSubmittedMillisecondTag => SupplierFulfillmentState.NeverSubmitted,
            ConfirmedFailureMillisecondTag => SupplierFulfillmentState.ConfirmedFailure,
            _ => SupplierFulfillmentState.UncertainOutcome
        };
    }

    public static bool IsSupplierOutcomeUncertain(TopupOrder order) =>
        GetSupplierState(order) == SupplierFulfillmentState.UncertainOutcome;

    public static bool IsSupplierConfirmedFailure(TopupOrder order) =>
        GetSupplierState(order) == SupplierFulfillmentState.ConfirmedFailure;

    public static bool IsSupplierNeverSubmitted(TopupOrder order) =>
        GetSupplierState(order) == SupplierFulfillmentState.NeverSubmitted;

    public static DateTime CreateNeverSubmittedTimestamp(DateTime utcTimestamp) =>
        WithMillisecondTag(utcTimestamp, NeverSubmittedMillisecondTag);

    public static DateTime CreateUncertainOutcomeTimestamp(DateTime utcTimestamp) =>
        WithMillisecondTag(utcTimestamp, UncertainOutcomeMillisecondTag);

    public static DateTime CreateConfirmedFailureTimestamp(DateTime utcTimestamp) =>
        WithMillisecondTag(utcTimestamp, ConfirmedFailureMillisecondTag);

    public static DateTime CreateStandardTimestamp(DateTime utcTimestamp) =>
        WithMillisecondTag(utcTimestamp, StandardProcessedMillisecondTag);

    private static DateTime WithMillisecondTag(DateTime timestamp, int millisecondTag)
    {
        var utc = timestamp.Kind == DateTimeKind.Utc
            ? timestamp
            : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        return new DateTime(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            utc.Second,
            millisecondTag,
            DateTimeKind.Utc);
    }
}
