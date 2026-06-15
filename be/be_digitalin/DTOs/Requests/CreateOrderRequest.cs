namespace be_digitalin.DTOs;

public record CreateOrderRequest(int ProductId, string TargetId, string? ZoneId, string? VoucherCode = null);