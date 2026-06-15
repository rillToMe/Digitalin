namespace be_digitalin.DTOs;

public record MidtransNotificationRequest(
    string order_id, string status_code, string gross_amount, 
    string transaction_status, string fraud_status, string signature_key);

public record DigiflazzWebhookPayload(DigiflazzWebhookData data);
public record DigiflazzWebhookData(string ref_id, string status, string sn, string message);