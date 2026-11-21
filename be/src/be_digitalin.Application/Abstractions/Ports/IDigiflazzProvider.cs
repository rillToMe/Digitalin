using System.Text.Json;

namespace be_digitalin.Application.Ports;

public enum DigiflazzTransactionOutcome
{
    Uncertain = 0,
    AcceptedPending = 1,
    Completed = 2,
    TerminalFailure = 3
}

public sealed record DigiflazzTransactionResult(
    DigiflazzTransactionOutcome Outcome,
    string Status,
    string Sn,
    string Message)
{
    public static DigiflazzTransactionResult Completed(string sn, string message = "") =>
        new(DigiflazzTransactionOutcome.Completed, "Sukses", sn, message);

    public static DigiflazzTransactionResult AcceptedPending(string message = "") =>
        new(DigiflazzTransactionOutcome.AcceptedPending, "Pending", string.Empty, message);

    public static DigiflazzTransactionResult TerminalFailure(string message) =>
        new(DigiflazzTransactionOutcome.TerminalFailure, "Gagal", string.Empty, message);

    public static DigiflazzTransactionResult Uncertain(string message, string status = "") =>
        new(DigiflazzTransactionOutcome.Uncertain, status, string.Empty, message);
}

/// <summary>
/// Dilempar ketika Digiflazz secara eksplisit mengembalikan status terminal <c>Gagal</c>.
/// </summary>
public sealed class DigiflazzTerminalFailureException : Exception
{
    public DigiflazzTerminalFailureException(string message)
        : base(string.IsNullOrWhiteSpace(message) ? "Transaksi ditolak oleh Digiflazz (status: Gagal)." : message)
    {
    }
}

/// <summary>
/// Dilempar ketika respons Digiflazz bersifat ambigu/tidak pasti (HTTP non-2xx,
/// timeout, body kosong, JSON tidak valid, field <c>data</c>/<c>status</c> hilang,
/// status tidak dikenal, atau <c>Sukses</c> tanpa <c>sn</c>).
/// </summary>
public sealed class DigiflazzUncertainOutcomeException : Exception
{
    public DigiflazzUncertainOutcomeException(string message, Exception? innerException = null)
        : base(string.IsNullOrWhiteSpace(message) ? "Status transaksi Digiflazz belum dapat dipastikan." : message, innerException)
    {
    }
}

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

    Task<(bool IsValid, string? Username, string Message)> CheckUsernameAsync(
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
