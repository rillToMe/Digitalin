using be_digitalin.Application.Validation;
using be_digitalin.DTOs;

namespace be_digitalin.Services.Payments;

/// <summary>
/// Konfigurasi metode pembayaran checkout: baca publik (hanya aktif) +
/// kelola admin (ikon/urutan/aktif). SSOT tampilan checkout — kemampuan
/// provider tetap di AppConstants.MidtransPaymentMethods.
/// </summary>
public interface IPaymentMethodService
{
    /// <summary>Metode + bank aktif untuk checkout, urut SortOrder.</summary>
    Task<IReadOnlyList<PaymentMethodResponse>> GetEnabledForCheckoutAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Semua metode + bank (termasuk nonaktif) untuk admin.</summary>
    Task<IReadOnlyList<AdminPaymentMethodResponse>> GetAllForAdminAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Update parsial metode. Null (not found) bila code tak dikenal.</summary>
    Task<AdminPaymentMethodResponse?> UpdateMethodAsync(
        string code,
        UpdatePaymentMethodRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Update parsial bank di bawah metode. Null bila metode/bank tak dikenal.</summary>
    Task<AdminPaymentBankResponse?> UpdateBankAsync(
        string methodCode,
        string bankCode,
        UpdatePaymentBankRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload ikon metode ke Cloudflare R2 (folder payment-icons) dan simpan
    /// URL publiknya. Null bila code tak dikenal. File lama dihapus dari R2.
    /// </summary>
    Task<string?> UploadMethodIconAsync(
        string code,
        ImageFileData file,
        CancellationToken cancellationToken = default);

    /// <summary>Upload ikon bank ke R2 — semantik sama dengan method.</summary>
    Task<string?> UploadBankIconAsync(
        string methodCode,
        string bankCode,
        ImageFileData file,
        CancellationToken cancellationToken = default);
}
