using System.Security.Claims;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace be_digitalin.Controllers;

/// <summary>
/// Cek Transaksi publik — TANPA auth (guest boleh). Boundary akses order:
///   - endpoint ini (publik): satu invoice by nomor, view model aman
///   - GET /orders/my-history: riwayat privat pemiliknya ([Authorize])
///   - /admin/orders: manajemen penuh (policy AdminOnly)
/// Optional-auth: kalau JWT valid ada, flag BelongsToAccount diisi supaya FE
/// bisa menandai "tersimpan di riwayat akun".
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/invoices")]
[Tags("Cek Transaksi (Public)")]
public class PublicInvoiceController : ControllerBase
{
    private readonly IInvoiceLookupService _invoiceLookupService;

    public PublicInvoiceController(IInvoiceLookupService invoiceLookupService)
    {
        _invoiceLookupService = invoiceLookupService;
    }

    // GET /api/v1/invoices/{orderNo} — publik, rate-limited per IP
    // (InvoiceLookupPolicy: read-only, budget cukup untuk polling FE).
    [HttpGet("{orderNo}")]
    [EnableRateLimiting("InvoiceLookupPolicy")]
    public async Task<ActionResult<PublicInvoiceResponse>> GetInvoice(
        string orderNo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return BadRequest(new MessageResponse("Masukkan nomor invoice."));
        }

        // Optional-auth (pola yang sama dengan POST /orders).
        int? customerId = null;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedId))
        {
            customerId = parsedId;
        }

        var invoice = await _invoiceLookupService.GetPublicInvoiceAsync(
            orderNo, customerId, cancellationToken);

        if (invoice is null)
        {
            // Pesan sama untuk "tidak ada" dan "bukan milik siapa pun" —
            // tidak membocorkan keberadaan order.
            return NotFound(new MessageResponse("Invoice tidak ditemukan."));
        }

        return Ok(invoice);
    }
}
