namespace be_digitalin.Domain.Entities;

/// <summary>
/// Konfigurasi metode pembayaran checkout (SSOT ikon + visibilitas).
/// Code = payment_type Midtrans Core API (qris/gopay/bank_transfer) —
/// kemampuan provider tetap di AppConstants.MidtransPaymentMethods;
/// entitas ini mengatur tampilan (ikon, nama, urutan, aktif).
/// </summary>
public class PaymentMethod
{
    public int Id { get; set; }

    // qris / gopay / bank_transfer
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }

    // URL publik ikon (umumnya Cloudflare R2) — bukan binary.
    public string? IconUrl { get; set; }

    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }

    public List<PaymentBank> Banks { get; set; } = [];
}

/// <summary>
/// Bank VA di bawah metode bank_transfer. Code = kode bank VA Midtrans
/// (bca/bni/bri/cimb/permata). Mandiri di-seed NONAKTIF: Midtrans
/// bank_transfer tidak menyediakan VA Mandiri (hanya echannel yang tidak
/// diimplementasi provider).
/// </summary>
public class PaymentBank
{
    public int Id { get; set; }

    // bca / bni / bri / cimb / permata / mandiri
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // URL publik ikon bank (umumnya Cloudflare R2) — bukan binary.
    public string? IconUrl { get; set; }

    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }

    public string PaymentMethodCode { get; set; } = string.Empty;
    public PaymentMethod? PaymentMethod { get; set; }
}
