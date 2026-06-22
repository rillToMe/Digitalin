namespace be_digitalin.DTOs;

public record CheckoutPreviewRequest(
    int ProductId,
    string? VoucherCode = null
);
