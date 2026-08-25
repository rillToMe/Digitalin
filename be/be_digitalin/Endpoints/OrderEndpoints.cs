using System.Security.Claims;
using System.Text.Json;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // AREA PUBLIK
        var publicGroup = app.MapGroup("/orders")
                             .WithTags("Transaksi & Checkout (Public)");

        publicGroup.MapPost("/", async (
            [FromBody] CreateOrderRequest req, 
            [FromServices] IOrderService orderService,
            ClaimsPrincipal user,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PlaceOrder");
            try {
                int? customerId = null;
                
                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedId))
                {
                    customerId = parsedId;
                }
                var result = await orderService.PlaceOrderAsync(req, customerId);
                return Results.Ok(result);
            } catch (Exception ex) {
                logger.LogWarning(ex, "[PlaceOrder] Failed for request.");
                var message = ex is InvalidOperationException || ex.Message.Length < 100
                    ? ex.Message
                    : "Terjadi kesalahan saat memproses pesanan Anda.";
                return Results.BadRequest(new { message });
            }
        });
        
        // POST /orders/preview — Checkout simulation (no order created)
        publicGroup.MapPost("/preview", async (
            [FromBody] CheckoutPreviewRequest req,
            [FromServices] ICheckoutService checkoutService) =>
        {
            try
            {
                var result = await checkoutService.PreviewAsync(req);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // GET /orders/my-history
        publicGroup.MapGet("/my-history", async (
            [FromQuery] int? page, 
            [FromQuery] int? limit,
            ClaimsPrincipal user, 
            ICustomerService service) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Results.Unauthorized();

            // Set nilai default pagination
            int currentPage = page ?? 1;
            int currentLimit = limit ?? 20;

            var history = await service.GetCustomerHistoryAsync(int.Parse(userIdClaim.Value), currentPage, currentLimit);
            return Results.Ok(history);
        }).RequireAuthorization();


        // AREA ADMIN
        var adminGroup = app.MapGroup("/admin/orders")
                            .WithTags("Manajemen Pesanan (Admin)")
                            .RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/", async ([FromServices] IOrderService orderService) => 
            Results.Ok(await orderService.GetAllOrdersAsync()));
        
        // POST /admin/orders/{orderNo}/retry
        adminGroup.MapPost("/{orderNo}/retry", async (
            string orderNo,
            [FromServices] IOrderService orderService) =>
        {
            var result = await orderService.RetryOrderAsync(orderNo);
            return MapOrderActionResult(result);
        });

        // POST /admin/orders/{orderNo}/refund - Ubah status jadi Refunded (Manual)
        adminGroup.MapPost("/{orderNo}/refund", async (
            string orderNo,
            [FromServices] IOrderService orderService) =>
        {
            var result = await orderService.RefundOrderAsync(orderNo);
            return MapOrderActionResult(result);
        });
    }

    /// <summary>
    /// Webhook Midtrans & Digiflazz — dipasang di root, di luar prefix versi:
    /// URL-nya sudah terdaftar di dashboard provider, jadi kontraknya harus stabil.
    /// </summary>
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var webhookGroup = app.MapGroup("/webhooks")
                              .WithTags("Webhooks (Sistem Integrasi)")
                              .AllowAnonymous();

        // Webhook Midtrans
        webhookGroup.MapPost("/midtrans", async ([FromBody] MidtransNotificationRequest req, [FromServices] IOrderService orderService) =>
        {
            try
            {
                await orderService.HandleNotificationAsync(req);
                return Results.Ok(new { message = "Notification received" });
            }
            catch (Exception)
            {
                return Results.Problem("Internal server error while processing notification.");
            }
        });
        
        
        // Webhook Digiflazz
        webhookGroup.MapPost("/digiflazz", async (
            HttpRequest req,
            [FromServices] IOrderService orderService,
            [FromServices] be_digitalin.Providers.DigiflazzProvider digiflazzProvider,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("DigiflazzWebhook");
            logger.LogInformation("[WEBHOOK] Ada panggilan masuk dari Digiflazz...");

            using var reader = new StreamReader(req.Body);
            var rawBody = await reader.ReadToEndAsync();
            var signatureHeader = req.Headers[AppConstants.HttpHeaders.XHubSignature].ToString();

            // Validasi Keamanan pakai Provider
            if (!digiflazzProvider.ValidateWebhookSignature(rawBody, signatureHeader))
            {
                logger.LogWarning("[WEBHOOK] Ditolak: Signature tidak valid!");
                return Results.Unauthorized();
            }

            var payload = JsonSerializer.Deserialize<DigiflazzWebhookPayload>(
                rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (payload?.data == null) return Results.Ok();

            var message = await orderService.HandleDigiflazzWebhookAsync(payload.data);
            return Results.Ok(new { message });
        });
    }

    // ── Helpers ─────────────────────────────────────────────────
    private static IResult MapOrderActionResult(OrderActionResult result) =>
        result.Outcome switch
        {
            OrderActionOutcome.Success      => Results.Ok(new { message = result.Message, status = result.OrderStatus }),
            OrderActionOutcome.NotFound     => Results.NotFound(new { message = result.Message }),
            OrderActionOutcome.InvalidState => Results.BadRequest(new { message = result.Message }),
            OrderActionOutcome.Conflict     => Results.Conflict(new { message = result.Message }),
            OrderActionOutcome.ServerError  => Results.Problem(result.Message),
            _                               => Results.BadRequest(new { message = result.Message }),
        };
}
