namespace be_digitalin.DTOs.Responses;

public record AdminVoucherItem(
    int Id,
    string Code,
    string DiscountType,
    decimal DiscountValue,
    decimal MinimumTransaction,
    decimal? MaximumDiscount,
    int UsageLimit,
    int UsedCount,
    DateTime ExpiredAt,
    bool IsActive,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AdminVoucherListResponse(
    IEnumerable<AdminVoucherItem> Items,
    int TotalCount,
    int CurrentPage,
    int TotalPages
);

public record VoucherUsageInfo(
    int OrderId,
    string OrderNo,
    string? CustomerName,
    string ProductName,
    decimal Price,
    decimal DiscountAmount,
    decimal FinalPrice,
    DateTime OrderDate,
    string OrderStatus
);

public record VoucherUsageResponse(
    IEnumerable<VoucherUsageInfo> Items,
    int TotalCount,
    int CurrentPage,
    int TotalPages
);

/// <summary>Respons PATCH /admin/vouchers/{id}/status — pengganti `new { message, isActive }`.</summary>
public record VoucherStatusResponse(string Message, bool IsActive);
