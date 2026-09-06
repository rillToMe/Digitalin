using be_digitalin.DTOs;

namespace be_digitalin.Services.Orders;

public interface IInvoiceLookupService
{
    /// <summary>
    /// Cari order berdasarkan nomor invoice untuk tampilan PUBLIK (Cek
    /// Transaksi tanpa login). Null bila invoice tidak ditemukan.
    /// `currentCustomerId` optional-auth: dipakai hanya untuk flag
    /// BelongsToAccount — tidak membatasi akses.
    /// </summary>
    Task<PublicInvoiceResponse?> GetPublicInvoiceAsync(
        string orderNo,
        int? currentCustomerId = null,
        CancellationToken cancellationToken = default);
}
