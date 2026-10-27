using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Orders;
using be_digitalin.Services.Settings;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Cek Transaksi publik — dua pintu, OrderNo tetap identifier bisnis (SSOT:
/// order_id Midtrans, ref_id Digiflazz, webhook, admin, my-history):
///   1. Resolve (POST /invoices/resolve): OrderNo (INV-yyyyMMdd-XXXXXXXXXX,
///      10 hex CSPRNG ~40 bit + rate limit per IP → tidak bisa dienumerasi)
///      → PublicHash saja — tanpa data transaksi.
///   2. Status (GET /invoices/{publicHash}): HANYA PublicHash — hex 64 dari
///      HMAC-SHA256(secret, OrderNo). OrderNo/format lain ditolak → null →
///      404 seragam. Keyed hash: mengetahui OrderNo tidak cukup untuk
///      merekonstruksi URL status tanpa lewat resolve.
/// Respons status hanya berisi view model aman (PublicInvoiceResponse).
/// </summary>
public class InvoiceLookupService : IInvoiceLookupService
{
    private readonly IApplicationDbContext _db;
    private readonly IAppSettingsService _appSettings;

    public InvoiceLookupService(IApplicationDbContext db, IAppSettingsService appSettings)
    {
        _db = db;
        _appSettings = appSettings;
    }

    public async Task<string?> ResolvePublicHashAsync(
        string orderNo,
        CancellationToken cancellationToken = default)
    {
        var normalized = orderNo.Trim();

        // Hanya proyeksikan kolom PublicHash — resolve tidak pernah membaca
        // atau mengembalikan data transaksi. Baris tanpa hash (pra-backfill)
        // diperlakukan tidak-ditemukan; startup backfill mengisinya.
        return await _db.TopupOrders
            .AsNoTracking()
            .Where(o => o.OrderNo == normalized && o.PublicHash != null)
            .Select(o => o.PublicHash!)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PublicInvoiceResponse?> GetPublicInvoiceAsync(
        string publicHash,
        int? currentCustomerId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = publicHash.Trim();

        // PublicHash = identifier publik SATU-SATUNYA yang diterima di sini.
        // OrderNo (URL lama /order/status/{OrderNo}) atau format lain → null
        // → 404 seragam — tidak membocorkan format mana yang benar.
        if (!OrderPublicHash.IsPublicHashFormat(normalized))
            return null;

        var hash = normalized.ToLowerInvariant();
        var order = await _db.TopupOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.PublicHash == hash, cancellationToken);

        if (order is null)
            return null;

        // Batas bayar hanya releven saat masih menunggu pembayaran; durasi
        // expiry dari AppSettings (SSOT yang sama dengan charge & sweep),
        // diper metode lewat OrderExpiryPolicy (cstore 24 jam — Midtrans
        // mengabaikan expiry custom di kanal itu).
        var createdAt = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);
        DateTime? expiresAt = null;
        if (order.Status == AppConstants.OrderStatus.Pending)
        {
            var expiryMinutes = await _appSettings.GetMidtransExpiryMinutesAsync();
            expiresAt = createdAt.AddMinutes(
                OrderExpiryPolicy.ExpiryMinutes(order.PaymentType, expiryMinutes));
        }

        return new PublicInvoiceResponse(
            order.OrderNo,
            order.ProductName,
            order.ImageUrl,
            order.BrandSlug,
            order.TotalPaid,
            order.Status,
            MapPaymentStatus(order.Status),
            createdAt,
            expiresAt,
            currentCustomerId is not null && order.CustomerId == currentCustomerId,
            // Metode + instruksi bayar — PENDING saja (aksi lanjut bayar di
            // halaman status, guest termasuk; terikat order+nominal spesifik).
            order.Status == AppConstants.OrderStatus.Pending ? order.PaymentType : null,
            order.Status == AppConstants.OrderStatus.Pending
                ? OrderPaymentInstructionsCodec.Deserialize(order.PaymentInstructionsJson)
                : null);
    }

    /// <summary>
    /// Status pembayaran ringkas dari state machine order:
    /// PENDING → UNPAID; AppConstants.OrderStatus.PaidStatuses (uang masuk)
    /// → PAID; EXPIRED/FAILED → FAILED; REFUNDED → REFUNDED.
    /// </summary>
    private static string MapPaymentStatus(string orderStatus) => orderStatus switch
    {
        AppConstants.OrderStatus.Pending => PaymentStatus.Unpaid,
        _ when AppConstants.OrderStatus.PaidStatuses.Contains(orderStatus) => PaymentStatus.Paid,
        AppConstants.OrderStatus.Expired or
        AppConstants.OrderStatus.Failed => PaymentStatus.Failed,
        AppConstants.OrderStatus.Refunded => PaymentStatus.Refunded,
        _ => PaymentStatus.Unpaid
    };
}

/// <summary>Nilai PaymentStatus pada PublicInvoiceResponse.</summary>
public static class PaymentStatus
{
    public const string Unpaid = "UNPAID";
    public const string Paid = "PAID";
    public const string Failed = "FAILED";
    public const string Refunded = "REFUNDED";
}
