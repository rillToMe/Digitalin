using be_digitalin.Constants;

namespace be_digitalin.Domain.Orders;

/// <summary>
/// SSOT durasi expiry order per metode pembayaran. Default = konfigurasi
/// admin (MIDRANS_EXPIRY, menit); cstore fixed 24 jam karena Midtrans
/// mengabaikan expiry custom untuk kanal itu (kode bayar hidup 24 jam).
/// Dipakai sweep (cutoff PENDING) dan invoice (expiresAt) supaya keduanya
/// tidak pernah mematikan order yang instruksinya masih berlaku.
/// </summary>
public static class OrderExpiryPolicy
{
    public static int ExpiryMinutes(string? paymentType, int defaultMinutes) =>
        paymentType == AppConstants.MidtransPaymentMethods.Cstore
            ? AppConstants.MidtransPaymentMethods.CstoreExpiryMinutes
            : defaultMinutes;
}
