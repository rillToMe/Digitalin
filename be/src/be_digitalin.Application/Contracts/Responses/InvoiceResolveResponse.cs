namespace be_digitalin.DTOs;

/// <summary>
/// Hasil resolusi Cek Transaksi: PublicHash satu-satunya identifier yang
/// diterima endpoint status publik (GET /invoices/{publicHash}). Sengaja
/// minimal — data transaksi lengkap hanya lewat endpoint status, bukan di sini.
/// </summary>
public record InvoiceResolveResponse(
    /// <summary>HMAC-SHA256(OrderNo) hex 64 — untuk URL /order/status/{publicHash}.</summary>
    string PublicHash
);
