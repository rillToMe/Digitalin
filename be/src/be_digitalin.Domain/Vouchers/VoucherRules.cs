using be_digitalin.Domain.Entities;

namespace be_digitalin.Domain.Commerce;

/// <summary>
/// Aturan murni voucher: komputasi status, validasi payload, kelayakan pakai,
/// dan perhitungan diskon. Tanpa IO/EF. Kode yang mengandung pesan
/// user-facing dipertahankan persis agar perilaku API tidak berubah.
/// </summary>
public static class VoucherRules
{
    /// <summary>Status voucher saat ini: Disabled &gt; Expired &gt; QuotaExceeded &gt; Active.</summary>
    public static string ComputeStatus(Voucher v)
    {
        if (!v.IsActive) return "Disabled";
        if (v.ExpiredAt <= DateTime.UtcNow) return "Expired";
        if (v.UsedCount >= v.UsageLimit) return "QuotaExceeded";
        return "Active";
    }

    /// <summary>
    /// Validasi payload create/update voucher. Melempar
    /// InvalidOperationException dengan pesan user-facing bila tidak valid.
    /// </summary>
    public static void ValidatePayload(string code, string discountType,
        decimal discountValue, int usageLimit, DateTime expiredAt, int? existingUsedCount = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 3 || code.Length > 50)
            throw new InvalidOperationException("Kode voucher harus antara 3 dan 50 karakter.");

        if (discountType != "Percentage" && discountType != "FixedAmount")
            throw new InvalidOperationException("Tipe diskon tidak valid. Pilih: Percentage atau FixedAmount.");

        if (discountValue <= 0)
            throw new InvalidOperationException("Nilai diskon harus lebih besar dari 0.");

        if (discountType == "Percentage" && discountValue > 100)
            throw new InvalidOperationException("Diskon persentase tidak boleh lebih dari 100%.");

        if (expiredAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Tanggal kedaluwarsa harus di masa depan.");

        if (usageLimit < 1)
            throw new InvalidOperationException("Batas penggunaan harus minimal 1.");

        if (existingUsedCount.HasValue && usageLimit < existingUsedCount.Value)
            throw new InvalidOperationException(
                $"Batas penggunaan tidak boleh kurang dari jumlah yang sudah digunakan ({existingUsedCount.Value}).");
    }

    /// <summary>
    /// Evaluasi kelayakan voucher terhadap nilai transaksi pada waktu tertentu.
    /// </summary>
    public static (Voucher? Voucher, string? Error) ValidateEligibility(
        Voucher? voucher,
        decimal transactionAmount,
        DateTime? nowUtc = null)
    {
        if (voucher == null)
            return (null, "Kode voucher tidak ditemukan.");

        if (!voucher.IsActive)
            return (null, "Voucher ini sudah tidak aktif.");

        var currentUtc = nowUtc ?? DateTime.UtcNow;
        if (currentUtc > voucher.ExpiredAt)
            return (null, "Voucher ini sudah kedaluwarsa.");

        if (voucher.UsedCount >= voucher.UsageLimit)
            return (null, "Kuota voucher ini sudah habis.");

        if (transactionAmount < voucher.MinimumTransaction)
            return (null, $"Voucher ini membutuhkan minimal transaksi sebesar Rp{voucher.MinimumTransaction:N0}.");

        return (voucher, null);
    }

    /// <summary>
    /// Hitung nominal potongan voucher dengan batas maksimum dan plafon harga produk,
    /// dibulatkan ke bilangan bulat Rupiah terdekat (<see cref="Pricing.RoundRupiah"/>).
    /// </summary>
    public static decimal CalculateDiscount(Voucher voucher, decimal productPrice)
    {
        var normalizedProductPrice = Pricing.RoundRupiah(productPrice);
        decimal discountAmount;

        if (voucher.DiscountType == "Percentage")
        {
            discountAmount = Pricing.RoundRupiah(normalizedProductPrice * (voucher.DiscountValue / 100m));
            if (voucher.MaximumDiscount.HasValue)
            {
                var maxDiscount = Pricing.RoundRupiah(voucher.MaximumDiscount.Value);
                if (discountAmount > maxDiscount)
                {
                    discountAmount = maxDiscount;
                }
            }
        }
        else // FixedAmount
        {
            discountAmount = Pricing.RoundRupiah(voucher.DiscountValue);
        }

        // Discount cannot exceed product price
        if (discountAmount > normalizedProductPrice)
            discountAmount = normalizedProductPrice;

        return discountAmount;
    }
}
