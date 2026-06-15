namespace be_digitalin.DTOs;

public record OrderResponse(
    int Id, string OrderNo, string PlayerId, string Status, 
    string ProductName, decimal Price, string? SnapToken, 
    string? RedirectUrl, DateTime OrderDate, string? Sn, 
    string? ImageUrl,
    decimal DiscountAmount = 0,
    decimal FinalPrice = 0
);