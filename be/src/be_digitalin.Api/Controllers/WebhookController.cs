using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 7: migrasi /webhooks (Midtrans & Digiflazz) dari Minimal API
/// (OrderEndpoints.cs) ke Controller. Dipasang di ROOT (tanpa prefix versi) —
/// URL terdaftar di dashboard provider sehingga kontrak path harus stabil.
/// Kedua action anonymous + validasi signature milik provider.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("/webhooks")]
[Tags("Webhooks (Sistem Integrasi)")]
public class WebhookController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IDigiflazzProvider _digiflazzProvider;
    private readonly ILoggerFactory _loggerFactory;

    public WebhookController(
        IOrderService orderService,
        IDigiflazzProvider digiflazzProvider,
        ILoggerFactory loggerFactory)
    {
        _orderService = orderService;
        _digiflazzProvider = digiflazzProvider;
        _loggerFactory = loggerFactory;
    }

    // POST /webhooks/midtrans
    [HttpPost("midtrans")]
    public async Task<IActionResult> Midtrans([FromBody] MidtransNotificationRequest req)
    {
        // Signature invalid = 401 (bukan 500) supaya Midtrans menghentikan retry
        // untuk payload palsu; kegagalan internal tetap 500 via Problem.
        try
        {
            if (!await _orderService.HandleNotificationAsync(req))
                return Unauthorized();

            return Ok(new MessageResponse("Notification received"));
        }
        catch (Exception)
        {
            return Problem("Internal server error while processing notification.");
        }
    }

    // POST /webhooks/digiflazz
    [HttpPost("digiflazz")]
    public async Task<IActionResult> Digiflazz()
    {
        var logger = _loggerFactory.CreateLogger("DigiflazzWebhook");
        logger.LogInformation("[WEBHOOK] Ada panggilan masuk dari Digiflazz...");

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();
        var signatureHeader = Request.Headers[AppConstants.HttpHeaders.XHubSignature].ToString();

        // Validasi Keamanan pakai Provider
        if (!_digiflazzProvider.ValidateWebhookSignature(rawBody, signatureHeader))
        {
            logger.LogWarning("[WEBHOOK] Ditolak: Signature tidak valid!");
            return Unauthorized();
        }

        var payload = JsonSerializer.Deserialize<DigiflazzWebhookPayload>(
            rawBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (payload?.data == null) return Ok();

        var message = await _orderService.HandleDigiflazzWebhookAsync(payload.data);
        return Ok(new MessageResponse(message));
    }
}
