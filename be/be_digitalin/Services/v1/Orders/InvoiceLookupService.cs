using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Domain.Entities;
using be_digitalin.Services.Settings;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Cek Transaksi publik — lookup order berdasarkan nomor invoice
/// (INV-yyyyMMdd-XXXXXXXXXX). Nomor invoice adalah id publik order yang
/// sudah aman dipakai (juga order_id Midtrans): 10 hex karakter dari
/// CSPRNG Guid (~40 bit) + rate limit per IP → tidak bisa dienumerasi,
/// dan tidak ada id DB sekuensial yang terekspos. Respons hanya berisi
/// view model aman (PublicInvoiceResponse) — bukan entity.
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

    public async Task<PublicInvoiceResponse?> GetPublicInvoiceAsync(
        string orderNo,
        int? currentCustomerId = null,
        CancellationToken cancellationToken = default)
    {
        var invoice = orderNo.Trim();

        // Lookup langsung oleh OrderNo — bukan Id DB sekuensial.
        var order = await _db.TopupOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNo == invoice, cancellationToken);

        if (order is null)
            return null;

        // Batas bayar hanya releven saat masih menunggu pembayaran; durasi
        // expiry Midtrans dari AppSettings (SSOT yang sama dengan Snap & sweep).
        DateTime? expiresAt = null;
        if (order.Status == AppConstants.OrderStatus.Pending)
        {
            var expiryMinutes = await _appSettings.GetMidtransExpiryMinutesAsync();
            expiresAt = order.OrderDate.AddMinutes(expiryMinutes);
        }

        return new PublicInvoiceResponse(
            order.OrderNo,
            order.ProductName,
            order.ImageUrl,
            order.TotalPaid,
            order.Status,
            MapPaymentStatus(order.Status),
            order.OrderDate,
            expiresAt,
            currentCustomerId is not null && order.CustomerId == currentCustomerId);
    }

    /// <summary>
    /// Status pembayaran ringkas dari state machine order:
    /// PENDING → UNPAID; PAID/PROCESSING/COMPLETED/NEEDS_ACTION → PAID
    /// (uang masuk); EXPIRED/FAILED → FAILED; REFUNDED → REFUNDED.
    /// </summary>
    private static string MapPaymentStatus(string orderStatus) => orderStatus switch
    {
        AppConstants.OrderStatus.Pending => PaymentStatus.Unpaid,
        AppConstants.OrderStatus.Paid or
        AppConstants.OrderStatus.Processing or
        AppConstants.OrderStatus.Completed or
        AppConstants.OrderStatus.NeedsAction => PaymentStatus.Paid,
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
