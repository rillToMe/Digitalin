using be_digitalin.Application.Validation;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Kelola konfigurasi metode pembayaran (ikon/urutan/aktif) — admin only.
/// Hanya field allow-list yang dapat diubah; Code adalah identitas permanen.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/payment-methods")]
[Tags("Manajemen Pembayaran (Admin)")]
public class AdminPaymentMethodController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;

    public AdminPaymentMethodController(IPaymentMethodService paymentMethodService)
    {
        _paymentMethodService = paymentMethodService;
    }

    // GET /api/v1/admin/payment-methods — semua metode + bank (termasuk nonaktif)
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminPaymentMethodResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var methods = await _paymentMethodService.GetAllForAdminAsync(cancellationToken);
        return Ok(methods);
    }

    // PUT /api/v1/admin/payment-methods/{code} — update parsial (DisplayName/IconUrl/Enabled/SortOrder)
    [HttpPut("{code}")]
    public async Task<ActionResult<AdminPaymentMethodResponse>> Update(
        string code,
        [FromBody] UpdatePaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _paymentMethodService.UpdateMethodAsync(code, request, cancellationToken);
            if (result is null)
                return NotFound(new MessageResponse($"Metode pembayaran '{code}' tidak ditemukan."));

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    // PUT /api/v1/admin/payment-methods/{code}/banks/{bankCode} — update parsial bank
    [HttpPut("{code}/banks/{bankCode}")]
    public async Task<ActionResult<AdminPaymentBankResponse>> UpdateBank(
        string code,
        string bankCode,
        [FromBody] UpdatePaymentBankRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _paymentMethodService.UpdateBankAsync(code, bankCode, request, cancellationToken);
            if (result is null)
                return NotFound(new MessageResponse(
                    $"Bank '{bankCode}' tidak ditemukan di bawah metode '{code}'."));

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    // POST /api/v1/admin/payment-methods/{code}/upload-icon — upload ikon (R2)
    // file nullable: kalau bagian file tidak dikirim, guard di bawah yang
    // menolak — sama seperti pola upload banner/produk.
    [HttpPost("{code}/upload-icon")]
    public async Task<IActionResult> UploadIcon(
        string code,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("File tidak boleh kosong.");

        if (file.Length > AppConstants.FileLimits.MaxImageBytes)
            return BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

        try
        {
            var imageUrl = await _paymentMethodService.UploadMethodIconAsync(
                code,
                new ImageFileData(file.FileName, file.ContentType, file.OpenReadStream()),
                cancellationToken);
            if (imageUrl == null)
                return NotFound(new MessageResponse($"Metode pembayaran '{code}' tidak ditemukan."));

            return Ok(new UploadImageResponse("Ikon metode pembayaran berhasil diperbarui!", imageUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    // POST /api/v1/admin/payment-methods/{code}/banks/{bankCode}/upload-icon
    [HttpPost("{code}/banks/{bankCode}/upload-icon")]
    public async Task<IActionResult> UploadBankIcon(
        string code,
        string bankCode,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("File tidak boleh kosong.");

        if (file.Length > AppConstants.FileLimits.MaxImageBytes)
            return BadRequest($"Ukuran file terlalu besar. Maksimal {AppConstants.FileLimits.MaxImageBytes / (1024 * 1024)} MB.");

        try
        {
            var imageUrl = await _paymentMethodService.UploadBankIconAsync(
                code,
                bankCode,
                new ImageFileData(file.FileName, file.ContentType, file.OpenReadStream()),
                cancellationToken);
            if (imageUrl == null)
                return NotFound(new MessageResponse(
                    $"Bank '{bankCode}' tidak ditemukan di bawah metode '{code}'."));

            return Ok(new UploadImageResponse("Ikon bank berhasil diperbarui!", imageUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }
}
