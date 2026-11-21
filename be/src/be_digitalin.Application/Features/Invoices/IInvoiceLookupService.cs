using be_digitalin.DTOs;

namespace be_digitalin.Services.Orders;

public interface IInvoiceLookupService
{
    /// <summary>
    /// Cari order untuk tampilan PUBLIK berdasar PublicHash (64 hex —
    /// identifier PUBLIK SATU-SATUYA yang diterima; OrderNo ditolak → null →
    /// 404). Null bila hash tidak ditemukan / format bukan hash.
    /// `currentCustomerId` optional-auth: dipakai hanya untuk flag
    /// BelongsToAccount — tidak membatasi akses.
    /// </summary>
    Task<PublicInvoiceResponse?> GetPublicInvoiceAsync(
        string publicHash,
        int? currentCustomerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cek Transaksi (publik): resolusi OrderNo (INV-...) → PublicHash untuk
    /// URL status. Null bila order tidak ditemukan atau PublicHash-nya belum
    /// terisi (baris pra-backfill — diisi idempotent saat startup).
    /// Tidak mengembalikan data transaksi apa pun — hanya hash.
    /// </summary>
    Task<string?> ResolvePublicHashAsync(
        string orderNo,
        CancellationToken cancellationToken = default);
}
