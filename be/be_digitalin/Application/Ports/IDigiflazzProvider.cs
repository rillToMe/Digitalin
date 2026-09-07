using System.Text.Json;

namespace be_digitalin.Application.Ports;

/// <summary>
/// Port Digiflazz (topup, price list, balance, webhook, check status).
/// Implemented by <c>be_digitalin.Providers.DigiflazzProvider</c>.
/// Mirrors the provider's public API 1:1 — delegation only, no logic here.
/// </summary>
public interface IDigiflazzProvider
{
    Task<string?> TopupAsync(
        string buyerSkuCode,
        string customerNo,
        string refId,
        CancellationToken cancellationToken = default);

    /// <summary>Price-list prepaid (kategori apa adanya — filter Games di caller).</summary>
    Task<List<DigiflazzPriceItem>> GetPriceListAsync(CancellationToken cancellationToken = default);

    Task<decimal> GetBalanceAsync(CancellationToken cancellationToken = default);

    bool ValidateWebhookSignature(string rawBody, string signatureHeader);

    Task<(string Status, string Sn, string Message)> CheckStatusAsync(
        string sku,
        string customerNo,
        string refId,
        CancellationToken cancellationToken = default);
}