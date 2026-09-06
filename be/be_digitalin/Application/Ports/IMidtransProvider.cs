namespace be_digitalin.Application.Ports;

/// <summary>
/// Port Midtrans (Snap transaction + signature webhook).
/// Implemented by <c>be_digitalin.Providers.MidtransProvider</c>.
/// </summary>
public interface IMidtransProvider
{
    /// <summary>Buat transaksi Snap dan dapatkan token pop-up UI.</summary>
    Task<(string? Token, string? RedirectUrl)> CreateSnapTransactionAsync(
        string orderNo,
        decimal grossAmount,
        string productId,
        string productName,
        string customerName,
        CancellationToken cancellationToken = default);

    bool ValidateSignature(
        string orderId,
        string statusCode,
        string grossAmount,
        string signatureKey);
}