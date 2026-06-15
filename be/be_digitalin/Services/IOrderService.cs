using be_digitalin.DTOs;

namespace be_digitalin.Services;

public record PagedResult<T>(IEnumerable<T> Data, int TotalItems, int TotalPages, int CurrentPage);

public interface IOrderService
{
    Task<OrderResponse> PlaceOrderAsync(CreateOrderRequest request, int? customerId = null);
    Task<PagedResult<OrderResponse>> GetAllOrdersAsync(int page = 1, int limit = 20, string? status = null);
    Task<PagedResult<OrderResponse>> GetOrdersAsync(int? customerId = null, int page = 1, int limit = 20, string? status = null);
    Task HandleNotificationAsync(MidtransNotificationRequest notification);
}