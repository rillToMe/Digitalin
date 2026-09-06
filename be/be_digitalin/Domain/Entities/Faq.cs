namespace be_digitalin.Domain.Entities;

/// <summary>
/// FAQ (Frequently Asked Question) — dikelola admin lewat CMS, ditampilkan
/// publik di halaman bantuan &amp; homepage. Kategori string polos (tanpa entity
/// kategori terpisah) — daftar kategori = nilai distinct di kolom ini.
/// </summary>
public class Faq
{
    public int Id { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    /// <summary>Kategori bebas (seed: Top Up, Pembayaran, Akun, Transaksi, Voucher, Lainnya).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>FAQ unggulan — tampil di homepage (maks 6, ditegakkan service).</summary>
    public bool Featured { get; set; }
}
