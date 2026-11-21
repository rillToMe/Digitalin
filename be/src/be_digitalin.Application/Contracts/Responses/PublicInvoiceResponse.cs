namespace be_digitalin.DTOs;

/// <summary>
/// View model publik untuk Cek Transaksi (tanpa login). Hanya field aman —
/// sengaja TIDAK memuat PlayerId, Sn, CustomerId, atau field admin.
/// Entity EF tidak pernah terekspos langsung.
/// </summary>
public record PublicInvoiceResponse(
    /// <summary>Invoice number = TopupOrder.OrderNo (id publik order).</summary>
    string InvoiceNumber,
    /// <summary>Snapshot nama produk (user-facing).</summary>
    string ProductName,
    /// <summary>Snapshot gambar produk (user-facing, nullable).</summary>
    string? ImageUrl,
    /// <summary>
    /// Slug katalog brand snapshot (identifier publik URL /game/{slug}).
    /// Hanya untuk fitur "Terakhir Dibeli" guest di FE — bukan bukti pembelian
    /// (BE divalidasi ulang vs katalog aktif). Null untuk order pra-kolom.
    /// </summary>
    string? BrandSlug,
    /// <summary>Amount = TotalPaid (harga final setelah diskon).</summary>
    decimal Amount,
    /// <summary>Status order mentah (PENDING/PAID/PROCESSING/.../REFUNDED).</summary>
    string Status,
    /// <summary>Status pembayaran ringkas: UNPAID / PAID / FAILED / REFUNDED.</summary>
    string PaymentStatus,
    /// <summary>Waktu order dibuat (UTC ISO).</summary>
    DateTime CreatedAt,
    /// <summary>Batas bayar Midtrans — hanya untuk PENDING, null selainnya.</summary>
    DateTime? ExpiresAt,
    /// <summary>True bila pemanggil terotentikasi sebagai pemilik order ini
    /// (optional-auth; FE menampilkan catatan "tersimpan di riwayat akun").</summary>
    bool BelongsToAccount,
    /// <summary>
    /// payment_type Midtrans Core API ("qris"/"gopay"/"bank_transfer") —
    /// HANYA diisi saat status PENDING (aksi lanjut bayar). Null selainnya.
    /// </summary>
    string? PaymentType = null,
    /// <summary>
    /// Instruksi bayar Core API (QR string / VA / deeplink) — HANYA saat
    /// PENDING, terikat order+nominal spesifik (bukan kredensial akun),
    /// level kepercayaan sama dengan POST /orders. Null selainnya.
    /// </summary>
    OrderPaymentInstructions? PaymentInstructions = null);
