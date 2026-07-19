namespace be_digitalin.DTOs;

public record CreateOrderRequest(
    int ProductId,
    Dictionary<string, string?> TargetValues,
    string? VoucherCode = null
);