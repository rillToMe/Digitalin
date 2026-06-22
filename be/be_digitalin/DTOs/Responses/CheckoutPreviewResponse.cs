namespace be_digitalin.DTOs;

public record CheckoutPreviewResponse(
    int ProductId,
    string ProductName,
    string? ImageUrl,
    string GameCode,
    decimal OriginalPrice,
    decimal DiscountAmount,
    decimal FinalPrice,
    string Currency,
    bool IsVoucherApplied,
    string? VoucherCode,
    string? DiscountType,
    decimal? DiscountValue,
    decimal? MaximumDiscount
);
