using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Content;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// FAQ publik (read-only, tanpa auth) — menggantikan data hardcode FE.
/// Bentuk respons: array FaqResponse (id, question, answer, category, featured).
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/faqs")]
[Tags("FAQ (Public)")]
public class FaqController : ControllerBase
{
    private readonly IFaqService _faqService;

    public FaqController(IFaqService faqService)
    {
        _faqService = faqService;
    }

    // GET /api/v1/faqs?category=...&search=...
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FaqResponse>>> GetFaqs(
        [FromQuery] string? category,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var faqs = await _faqService.GetFaqsAsync(category, search, cancellationToken);
        return Ok(faqs);
    }

    // GET /api/v1/faqs/featured — homepage, maks 6 (ditegakkan service)
    [HttpGet("featured")]
    public async Task<ActionResult<IEnumerable<FaqResponse>>> GetFeaturedFaqs(
        CancellationToken cancellationToken)
    {
        var faqs = await _faqService.GetFeaturedFaqsAsync(cancellationToken);
        return Ok(faqs);
    }

    // GET /api/v1/faqs/categories — kategori distinct (FE tidak hardcode)
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = await _faqService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }
}
