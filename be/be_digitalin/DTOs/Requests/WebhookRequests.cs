namespace be_digitalin.DTOs;

public record MidtransNotificationRequest(
    string order_id, string status_code, string gross_amount,
    string transaction_status, string signature_key,
    /// <summary>fraud_status TIDAK selalu dikirim Midtrans (notifikasi
    /// settlement cstore terverifikasi tanpa field ini). Null = jangan tolak
    /// di notifikasi — keputusan fraud tetap dipegang konfirmasi status API
    /// (GET /v2/{order_id}/status selalu memuat fraud_status).</summary>
    string? fraud_status = null,
    /// <summary>payment_type aktual (mis. "qris", "gopay", "bank_transfer") —
    /// mengisi PaymentType order bila belum tercatat (backfill order lama).</summary>
    string? payment_type = null,
    /// <summary>transaction_id Midtrans — kunci rekonsiliasi selain order_id.</summary>
    string? transaction_id = null,
    string? transaction_time = null,
    string? settlement_time = null);

public record DigiflazzWebhookPayload(DigiflazzWebhookData data);
public record DigiflazzWebhookData(string ref_id, string status, string sn, string message);