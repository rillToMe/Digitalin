using be_digitalin.DTOs;

namespace be_digitalin.Services;

public record PagedResult<T>(IEnumerable<T> Data, int TotalItems, int TotalPages, int CurrentPage);

/// <summary>Outcome of an admin order action (retry/refund) — mapped to HTTP by the endpoint.</summary>
public enum OrderActionOutcome
{
    Success,
    NotFound,
    InvalidState,
    Conflict,
    Error,
    ServerError
}

public record OrderActionResult(
    OrderActionOutcome Outcome,
    string Message,
    string? OrderStatus = null);

public interface IOrderService
{
    Task<OrderResponse> PlaceOrderAsync(CreateOrderRequest request, int? customerId = null);
    Task<PagedResult<OrderResponse>> GetAllOrdersAsync(int page = 1, int limit = 20, string? status = null);
    Task<PagedResult<OrderResponse>> GetOrdersAsync(int? customerId = null, int page = 1, int limit = 20, string? status = null);
    Task HandleNotificationAsync(MidtransNotificationRequest notification);

    /// <summary>Admin: retry a stuck (NEEDS_ACTION) order against Digiflazz.</summary>
    Task<OrderActionResult> RetryOrderAsync(string orderNo);

    /// <summary>Admin: mark a stuck (NEEDS_ACTION) order as REFUNDED.</summary>
    Task<OrderActionResult> RefundOrderAsync(string orderNo);

    /// <summary>Webhook: apply a Digiflazz status update to the matching order. Returns a status message for the response body.</summary>
    Task<string> HandleDigiflazzWebhookAsync(DigiflazzWebhookData data);
}
