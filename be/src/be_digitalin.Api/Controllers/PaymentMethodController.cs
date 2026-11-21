using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Payments;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Metode pembayaran checkout (publik) — hanya item aktif + ikon.
/// Sumber data kartu metode/bank di halaman checkout FE.
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/payment-methods")]
[Tags("Checkout & Pembayaran (Public)")]
public class PaymentMethodController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;

    public PaymentMethodController(IPaymentMethodService paymentMethodService)
    {
        _paymentMethodService = paymentMethodService;
    }

    // GET /api/v1/payment-methods — metode aktif + bank aktif (untuk bank_transfer)
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodResponse>>> GetEnabled(
        CancellationToken cancellationToken)
    {
        var methods = await _paymentMethodService.GetEnabledForCheckoutAsync(cancellationToken);
        return Ok(methods);
    }
}
