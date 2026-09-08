namespace be_digitalin.Application.Ports;

/// <summary>
/// Permintaan charge Midtrans Core API (POST /v2/charge). PaymentType adalah
/// payment_type Midtrans (mis. "qris", "gopay", "bank_transfer", "cstore").
/// Bank hanya relevan untuk bank_transfer — nama bank VA (bca/bni/bri/cimb;
/// mandiri dipetakan provider ke echannel). Store hanya relevan untuk
/// cstore — toko tujuan (indomaret/alfamart).
/// </summary>
public sealed record MidtransChargeRequest(
    string OrderNo,
    decimal GrossAmount,
    string ProductId,
    string ProductName,
    string CustomerName,
    string PaymentType,
    string? Bank = null,
    string? Store = null);
