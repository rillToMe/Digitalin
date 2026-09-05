using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 6: migrasi /admin/orders dari Minimal API (OrderEndpoints.cs) ke
/// Controller. Otorisasi AdminOnly di level class; retry/refund memakai
/// pemetaan outcome → HTTP yang sama dengan MapOrderActionResult lama.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/orders")]
[Tags("Manajemen Pesanan (Admin)")]
public class AdminOrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public AdminOrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // GET /api/v1/admin/orders
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponse>>> List()
    {
        var result = await _orderService.GetAllOrdersAsync();
        return Ok(result);
    }

    // POST /api/v1/admin/orders/{orderNo}/retry
    [HttpPost("{orderNo}/retry")]
    public async Task<IActionResult> Retry(string orderNo)
    {
        var result = await _orderService.RetryOrderAsync(orderNo);
        return MapOrderActionResult(result);
    }

    // POST /api/v1/admin/orders/{orderNo}/refund — ubah status jadi Refunded (manual)
    [HttpPost("{orderNo}/refund")]
    public async Task<IActionResult> Refund(string orderNo)
    {
        var result = await _orderService.RefundOrderAsync(orderNo);
        return MapOrderActionResult(result);
    }

    // ── Helpers ─────────────────────────────────────────────────
    // ServerError → Problem() agar bentuknya ProblemDetails, sama seperti
    // Results.Problem() di endpoint Minimal API lama.
    private IActionResult MapOrderActionResult(OrderActionResult result) =>
        result.Outcome switch
        {
            OrderActionOutcome.Success      => Ok(new OrderActionResponse(result.Message, result.OrderStatus!)),
            OrderActionOutcome.NotFound     => NotFound(new MessageResponse(result.Message)),
            OrderActionOutcome.InvalidState => BadRequest(new MessageResponse(result.Message)),
            OrderActionOutcome.Conflict     => Conflict(new MessageResponse(result.Message)),
            OrderActionOutcome.ServerError  => Problem(result.Message),
            _                               => BadRequest(new MessageResponse(result.Message)),
        };
}
