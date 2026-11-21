namespace be_digitalin.Application.Ports;

/// <summary>
/// Port Midtrans Core API: charge/status/cancel/expire + signature webhook.
/// Implemented by <c>be_digitalin.Providers.MidtransProvider</c>.
/// </summary>
public interface IMidtransProvider
{
    /// <summary>
    /// Buat transaksi Core API (POST /v2/charge) untuk payment_type spesifik
    /// dan kembalikan instruksi pembayarannya (QRIS/VA/e-wallet).
    /// </summary>
    Task<MidtransChargeResult> CreateChargeAsync(
        MidtransChargeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validasi signature_key notifikasi webhook:
    /// SHA512(order_id + status_code + gross_amount + ServerKey).
    /// </summary>
    bool ValidateSignature(
        string orderId,
        string statusCode,
        string grossAmount,
        string signatureKey);

    /// <summary>
    /// Ambil status transaksi dari sisi Midtrans (GET /v2/{order_id}/status).
    /// Null bila transaksi tidak dikenal Midtrans (404).
    /// </summary>
    Task<MidtransTransactionStatus?> GetStatusAsync(
        string orderNo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Minta pembatalan transaksi sebelum settlement
    /// (POST /v2/{order_id}/cancel). False bila Midtrans menolak secara sadar
    /// (mis. sudah settlement atau order tidak dikenal).
    /// </summary>
    Task<bool> CancelAsync(
        string orderNo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Paksa transaksi PENDING hangus lebih awal
    /// (POST /v2/{order_id}/expire). Semantik hasil sama dengan CancelAsync.
    /// </summary>
    Task<bool> ExpireAsync(
        string orderNo,
        CancellationToken cancellationToken = default);
}