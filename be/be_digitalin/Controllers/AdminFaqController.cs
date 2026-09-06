using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.Services.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Manajemen FAQ (admin) — CRUD penuh, auth policy AdminOnly yang sama
/// dengan controller admin lain. Id tidak pernah diterima dari klien.
/// Kontrak error: validasi/batas-unggulan → 400 { message } (pola Voucher),
/// id tidak ada → 404 { message }.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/faqs")]
[Tags("Manajemen FAQ (Admin)")]
public class AdminFaqController : ControllerBase
{
    private readonly IFaqService _faqService;

    public AdminFaqController(IFaqService faqService)
    {
        _faqService = faqService;
    }

    // GET /api/v1/admin/faqs?search=...&category=...
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FaqResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var faqs = await _faqService.GetFaqsAsync(category, search, cancellationToken);
        return Ok(faqs);
    }

    // GET /api/v1/admin/faqs/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<FaqResponse>> Detail(int id, CancellationToken cancellationToken)
    {
        var faq = await _faqService.GetFaqByIdAsync(id, cancellationToken);
        if (faq == null)
            return NotFound(new MessageResponse("FAQ tidak ditemukan."));

        return Ok(faq);
    }

    // POST /api/v1/admin/faqs
    [HttpPost]
    public async Task<ActionResult<FaqResponse>> Create(
        [FromBody] CreateFaqRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var faq = await _faqService.CreateFaqAsync(request, cancellationToken);
            return Created($"{AppConstants.Api.Prefix}/admin/faqs/{faq.Id}", faq);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // PUT /api/v1/admin/faqs/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<FaqResponse>> Update(
        int id,
        [FromBody] UpdateFaqRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var faq = await _faqService.UpdateFaqAsync(id, request, cancellationToken);
            if (faq == null)
                return NotFound(new MessageResponse("FAQ tidak ditemukan."));

            return Ok(faq);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // DELETE /api/v1/admin/faqs/{id} — hard delete
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var success = await _faqService.DeleteFaqAsync(id, cancellationToken);
        if (!success)
            return NotFound(new MessageResponse("FAQ tidak ditemukan."));

        return Ok(new MessageResponse("FAQ berhasil dihapus."));
    }
}
