using System.Security.Claims;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Customer;
using be_digitalin.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 6: migrasi /orders publik dari Minimal API (OrderEndpoints.cs) ke
/// Controller. POST /orders bersifat optional-auth: kalau ada klaim
/// NameIdentifier yang valid, order dikaitkan ke customer — kalau tidak,
/// customerId = null (alur anonymous). Perilaku sama persis dengan endpoint lama.
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/orders")]
[Tags("Transaksi & Checkout (Public)")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICheckoutService _checkoutService;
    private readonly ICustomerService _customerService;
    private readonly ILoggerFactory _loggerFactory;

    public OrderController(
        IOrderService orderService,
        ICheckoutService checkoutService,
        ICustomerService customerService,
        ILoggerFactory loggerFactory)
    {
        _orderService = orderService;
        _checkoutService = checkoutService;
        _customerService = customerService;
        _loggerFactory = loggerFactory;
    }

    // POST /api/v1/orders — buat pesanan (optional-auth, rate-limited per IP)
    [HttpPost]
    [EnableRateLimiting("OrderPolicy")]
    public async Task<IActionResult> PlaceOrder([FromBody] CreateOrderRequest req)
    {
        var logger = _loggerFactory.CreateLogger("PlaceOrder");
        try
        {
            int? customerId = null;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedId))
            {
                customerId = parsedId;
            }

            var result = await _orderService.PlaceOrderAsync(req, customerId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // Pelanggaran aturan bisnis → 400 dengan pesan kontrak.
            logger.LogWarning(ex, "[PlaceOrder] Request ditolak aturan bisnis.");
            return BadRequest(new MessageResponse(ex.Message));
        }
        catch (Exception ex)
        {
            // Kegagalan non-bisnis (DB, provider) → 500 generik. Pesan internal
            // tidak boleh bocor ke klien.
            logger.LogError(ex, "[PlaceOrder] Unexpected error.");
            return Problem("Terjadi kesalahan saat memproses pesanan Anda.");
        }
    }

    // POST /api/v1/orders/preview — simulasi checkout (tanpa membuat order)
    [HttpPost("preview")]
    public async Task<ActionResult<CheckoutPreviewResponse>> Preview([FromBody] CheckoutPreviewRequest req)
    {
        try
        {
            var result = await _checkoutService.PreviewAsync(req);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // GET /api/v1/orders/my-history
    [HttpGet("my-history")]
    [Authorize]
    public async Task<ActionResult<PagedResult<OrderResponse>>> MyHistory(
        [FromQuery] int? page,
        [FromQuery] int? limit)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null) return Unauthorized();

        // Set nilai default pagination + clamp (page < 1 atau limit di luar
        // 1..100 → default aman; Skip negatif = error EF, limit raksasa = OOM).
        int currentPage = Math.Max(page ?? 1, 1);
        int currentLimit = Math.Clamp(limit ?? 20, 1, 100);

        var history = await _customerService.GetCustomerHistoryAsync(
            int.Parse(userIdClaim.Value), currentPage, currentLimit);
        return Ok(history);
    }
}
