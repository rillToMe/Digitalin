namespace be_digitalin.DTOs;

/// <summary>
/// View model publik untuk Cek Transaksi (tanpa login). Hanya field aman —
/// sengaja TIDAK memuat PlayerId, SnapToken, RedirectUrl, Sn, CustomerId,
/// atau field admin. Entity EF tidak pernah terekspos langsung.
/// </summary>
public record PublicInvoiceResponse(
    /// <summary>Invoice number = TopupOrder.OrderNo (id publik order).</summary>
    string InvoiceNumber,
    /// <summary>Snapshot nama produk (user-facing).</summary>
    string ProductName,
    /// <summary>Snapshot gambar produk (user-facing, nullable).</summary>
    string? ImageUrl,
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
    /// Token pembayaran Snap Midtrans — HANYA diisi saat status PENDING, agar
    /// halaman status bisa melanjutkan pembayaran (termasuk guest). Level
    /// kepercayaan sama dengan POST /orders (guest checkout menerima token
    /// ini juga): terikat order+nominal spesifik, bukan kredensial akun.
    /// Null untuk semua status lain.
    /// </summary>
    string? SnapToken = null);
