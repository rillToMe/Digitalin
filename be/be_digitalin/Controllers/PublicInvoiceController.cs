using System.Security.Claims;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace be_digitalin.Controllers;

/// <summary>
/// Cek Transaksi publik — TANPA auth (guest boleh). Boundary akses order:
///   - POST /invoices/resolve (publik): OrderNo → PublicHash saja
///   - GET /invoices/{publicHash} (publik): view model aman — PublicHash
///     adalah identifier publik SATU-SATUNYA (OrderNo → 404)
///   - GET /orders/my-history: riwayat privat pemiliknya ([Authorize])
///   - /admin/orders: manajemen penuh (policy AdminOnly)
/// Optional-auth di GET: kalau JWT valid ada, flag BelongsToAccount diisi
/// supaya FE bisa menandai "tersimpan di riwayat akun".
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

    // POST /api/v1/invoices/resolve — Cek Transaksi: OrderNo → PublicHash.
    // Publik + rate-limited (InvoiceLookupPolicy) — endpoint ini JALUR
    // enumerasi OrderNo, jadi limit per-IP wajib melekat di sini juga.
    // Body (bukan query) supaya nomor invoice tidak tercatat di access log.
    // Respons minimal: hanya PublicHash — data transaksi tetap lewat GET.
    [HttpPost("resolve")]
    [EnableRateLimiting("InvoiceLookupPolicy")]
    public async Task<ActionResult<InvoiceResolveResponse>> Resolve(
        [FromBody] ResolveInvoiceRequest req,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.OrderNo))
        {
            return BadRequest(new MessageResponse("Masukkan nomor invoice."));
        }

        var publicHash = await _invoiceLookupService.ResolvePublicHashAsync(
            req.OrderNo, cancellationToken);

        if (publicHash is null)
        {
            // Pesan sama dengan GET — tidak membocorkan keberadaan order.
            return NotFound(new MessageResponse("Invoice tidak ditemukan."));
        }

        return Ok(new InvoiceResolveResponse(publicHash));
    }

    // GET /api/v1/invoices/{publicHash} — publik, rate-limited per IP
    // (InvoiceLookupPolicy: read-only, budget cukup untuk polling FE).
    // PublicHash (64 hex) satu-satunya format yang diterima; OrderNo /
    // bookmark URL lama /order/status/{OrderNo} → 404 seragam.
    [HttpGet("{publicHash}")]
    [EnableRateLimiting("InvoiceLookupPolicy")]
    public async Task<ActionResult<PublicInvoiceResponse>> GetInvoice(
        string publicHash,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicHash))
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
            publicHash, customerId, cancellationToken);

        if (invoice is null)
        {
            // Pesan sama untuk "tidak ada" dan "format salah" — tidak
            // membocorkan keberadaan order.
            return NotFound(new MessageResponse("Invoice tidak ditemukan."));
        }

        return Ok(invoice);
    }
}
