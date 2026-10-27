using System.Security.Cryptography;
using System.Text;

namespace be_digitalin.Services.Orders;

/// <summary>
/// SSOT derivasi PublicHash — representasi OrderNo untuk URL status publik
/// (/order/status/{publicHash}): hex kecil 64 karakter dari
/// HMAC-SHA256(secretKey, OrderNo). OrderNo tetap satu-satunya identifier
/// bisnis/publik (Midtrans order_id, Digiflazz ref_id, kontrak API);
/// PublicHash murni representasi turunan — bukan sistem token terpisah.
/// Keyed (bukan SHA256 polos) agar mengetahui OrderNo tidak cukup untuk
/// merekonstruksi URL status. Dipakai OrderService (order baru), backfill
/// startup, dan tes — satu helper, tanpa duplikasi.
/// </summary>
internal static class OrderPublicHash
{
    /// <summary>Hex kecil 64 karakter — format terpisah dari OrderNo (INV-...).</summary>
    internal static string Compute(string orderNo, string secretKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        return Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(orderNo))).ToLowerInvariant();
    }

    /// <summary>
    /// Deteksi format PublicHash: 64 karakter hex (case-insensitive —
    /// input dinormalisasi ke huruf kecil oleh pemanggil).
    /// </summary>
    internal static bool IsPublicHashFormat(string reference) =>
        reference.Length == 64 && reference.All(Uri.IsHexDigit);
}
