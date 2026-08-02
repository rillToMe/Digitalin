using be_digitalin.DTOs.Requests;
using be_digitalin.DTOs.Responses;
using be_digitalin.Models;

namespace be_digitalin.Services;

public interface IVoucherService
{
    // ── Admin CRUD ────────────────────────────────────────────────
    Task<AdminVoucherListResponse> GetVouchersAsync(
        string? search, string? status, string? sortBy, string? sortOrder,
        int page, int limit);

    Task<AdminVoucherItem?> GetVoucherByIdAsync(int id);

    Task<AdminVoucherItem> CreateVoucherAsync(CreateVoucherRequest request);

    Task<AdminVoucherItem?> UpdateVoucherAsync(int id, UpdateVoucherRequest request);

    Task<bool?> UpdateVoucherStatusAsync(int id, UpdateVoucherStatusRequest request);

    Task<(bool Success, string? ErrorMessage)> DeleteVoucherAsync(int id);

    Task<VoucherUsageResponse> GetVoucherUsageAsync(int id, int page, int limit);

    // ── Shared Voucher Logic (used by Preview & Create Order) ─────
    /// <summary>
    /// Validates voucher without reserving quota. Returns null + error if invalid.
    /// </summary>
    Task<(Voucher? Voucher, string? Error)> ValidateVoucherAsync(string code, decimal transactionAmount);

    /// <summary>
    /// Pure discount calculation. No DB access. Shared by Preview and Order.
    /// </summary>
    decimal CalculateDiscount(Voucher voucher, decimal productPrice);

    /// <summary>
    /// Reserves voucher quota (increments UsedCount). Call only during order creation.
    /// </summary>
    Task ReserveVoucherAsync(Voucher voucher);

    /// <summary>
    /// Releases a previously reserved quota (decrements UsedCount, only when &gt; 0).
    /// Call when an order reaches a terminal state without successful fulfillment
    /// (Expired / Cancel / Deny / Refunded). Idempotency is guaranteed by the caller's
    /// order status guards — this method must not be relied on as the sole guard.
    /// </summary>
    Task ReleaseVoucherAsync(string voucherCode);
}
