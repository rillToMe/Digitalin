namespace be_digitalin.DTOs.Requests;

public record CreateVoucherRequest(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    decimal MinimumTransaction,
    decimal? MaximumDiscount,
    int UsageLimit,
    DateTime ExpiredAt,
    bool IsActive
);

public record UpdateVoucherRequest(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    decimal MinimumTransaction,
    decimal? MaximumDiscount,
    int UsageLimit,
    DateTime ExpiredAt,
    bool IsActive
);

public record UpdateVoucherStatusRequest(
    bool IsActive
);
