using be_digitalin.Constants;

namespace be_digitalin.Domain.Payments;

/// <summary>
/// Aturan domain murni untuk validasi pilihan kanal pembayaran, sub-kanal bank VA,
/// dan gerai convenience store sebelum order menyentuh database atau gateway pembayaran.
/// </summary>
public static class PaymentMethodRules
{
    public static (string PaymentMethod, string? PaymentBank, string? PaymentStore) ValidateSelection(
        string? rawPaymentMethod,
        string? rawPaymentBank,
        string? rawPaymentStore)
    {
        string? paymentMethod = rawPaymentMethod?.Trim().ToLowerInvariant();
        string? paymentBank = rawPaymentBank?.Trim().ToLowerInvariant();
        string? paymentStore = rawPaymentStore?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(paymentMethod))
        {
            throw new InvalidOperationException(
                "Metode pembayaran wajib dipilih: qris, gopay, atau bank_transfer.");
        }

        bool knownMethod =
            paymentMethod == AppConstants.MidtransPaymentMethods.Qris ||
            paymentMethod == AppConstants.MidtransPaymentMethods.GoPay ||
            paymentMethod == AppConstants.MidtransPaymentMethods.ShopeePay ||
            paymentMethod == AppConstants.MidtransPaymentMethods.BankTransfer ||
            paymentMethod == AppConstants.MidtransPaymentMethods.Cstore;

        if (!knownMethod)
        {
            throw new InvalidOperationException(
                "Metode pembayaran tidak didukung. Gunakan: qris, gopay, atau bank_transfer.");
        }

        if (paymentMethod == AppConstants.MidtransPaymentMethods.BankTransfer)
        {
            if (string.IsNullOrEmpty(paymentBank))
                throw new InvalidOperationException(
                    "Pembayaran bank_transfer wajib memilih bank VA (bca/bni/bri/cimb/permata/mandiri/bsi).");

            if (!AppConstants.MidtransPaymentMethods.VaBanks.Contains(paymentBank))
                throw new InvalidOperationException(
                    $"Bank VA tidak didukung: {paymentBank}. Pilih bca/bni/bri/cimb/permata/mandiri/bsi.");
        }
        else if (!string.IsNullOrEmpty(paymentBank))
        {
            throw new InvalidOperationException(
                "paymentBank hanya berlaku untuk paymentMethod bank_transfer.");
        }

        if (paymentMethod == AppConstants.MidtransPaymentMethods.Cstore)
        {
            if (string.IsNullOrEmpty(paymentStore))
                throw new InvalidOperationException(
                    "Pembayaran cstore wajib memilih toko (indomaret/alfamart).");

            if (!AppConstants.MidtransPaymentMethods.CstoreStores.Contains(paymentStore))
                throw new InvalidOperationException(
                    $"Toko tidak didukung: {paymentStore}. Pilih indomaret/alfamart.");
        }
        else if (!string.IsNullOrEmpty(paymentStore))
        {
            throw new InvalidOperationException(
                "paymentStore hanya berlaku untuk paymentMethod cstore.");
        }

        return (paymentMethod, paymentBank, paymentStore);
    }
}
