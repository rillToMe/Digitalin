namespace be_digitalin.DTOs;

public record OrderResponse(
    int Id, string OrderNo, string PlayerId, string Status,
    string ProductName, decimal Price,
    DateTime OrderDate, string? Sn,
    string? ImageUrl,
    decimal DiscountAmount = 0,
    decimal FinalPrice = 0,
    /// <summary>payment_type Midtrans Core API ("qris"/"gopay"/"bank_transfer").</summary>
    string? PaymentType = null,
    /// <summary>Instruksi bayar Core API (QRIS/VA/e-wallet).</summary>
    OrderPaymentInstructions? PaymentInstructions = null,
    /// <summary>
    /// HMAC-SHA256(OrderNo) hex 64 char — representasi URL status publik
    /// (/order/status/{publicHash}). Null untuk baris pra-backfill.
    /// OrderNo tetap identifier bisnis.
    /// </summary>
    string? PublicHash = null
);