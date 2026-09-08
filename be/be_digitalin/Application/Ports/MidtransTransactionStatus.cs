namespace be_digitalin.Application.Ports;

/// <summary>
/// Status transaksi Midtrans dari GET /v2/{order_id}/status — untuk verifikasi
/// ulang sebelum menandai PAID (Phase 3) dan rekonsiliasi manual.
/// </summary>
public sealed record MidtransTransactionStatus(
    string? TransactionId,
    string? OrderId,
    string? PaymentType,
    string? TransactionStatus,
    string? FraudStatus,
    string? StatusCode,
    string? GrossAmount,
    string? SettlementTime);
