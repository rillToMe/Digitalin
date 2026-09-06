using be_digitalin.Domain.Entities;

namespace be_digitalin.Domain.Commerce;

/// <summary>
/// Aturan murni voucher: komputasi status & validasi payload (Fase 5 —
/// diekstrak dari VoucherService). Tanpa IO/EF. Kode yang mengandung pesan
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
}