using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.DTOs.Responses;
using be_digitalin.Services.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 3: migrasi GET /admin/vouchers (list, detail, usage) dari Minimal API
/// (VoucherEndpoints.cs) ke Controller. POST/PUT/PATCH/DELETE masih Minimal
/// API sampai Fase 4 — tidak ada route yang dobel karena metodenya berbeda.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/vouchers")]
[Tags("Manajemen Voucher (Admin)")]
public class VoucherController : ControllerBase
{
    private readonly IVoucherService _voucherService;

    public VoucherController(IVoucherService voucherService)
    {
        _voucherService = voucherService;
    }

    // GET /api/v1/admin/vouchers — list dengan pagination, search, filter, sort
    [HttpGet]
    public async Task<ActionResult<AdminVoucherListResponse>> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var result = await _voucherService.GetVouchersAsync(search, status, sortBy, sortOrder, page, limit);
        return Ok(result);
    }

    // GET /api/v1/admin/vouchers/{id} — detail
    [HttpGet("{id}")]
    public async Task<ActionResult<AdminVoucherItem>> Detail(int id)
    {
        var voucher = await _voucherService.GetVoucherByIdAsync(id);
        if (voucher == null)
            return NotFound(new MessageResponse("Voucher tidak ditemukan."));

        return Ok(voucher);
    }

    // GET /api/v1/admin/vouchers/{id}/usage — riwayat pemakaian
    [HttpGet("{id}/usage")]
    public async Task<ActionResult<VoucherUsageResponse>> Usage(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        try
        {
            var result = await _voucherService.GetVoucherUsageAsync(id, page, limit);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new MessageResponse(ex.Message!));
        }
    }

    // POST /api/v1/admin/vouchers — buat
    [HttpPost]
    public async Task<ActionResult<AdminVoucherItem>> Create([FromBody] CreateVoucherRequest request)
    {
        try
        {
            var voucher = await _voucherService.CreateVoucherAsync(request);
            return Created($"{AppConstants.Api.Prefix}/admin/vouchers/{voucher.Id}", voucher);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // PUT /api/v1/admin/vouchers/{id} — update penuh
    [HttpPut("{id}")]
    public async Task<ActionResult<AdminVoucherItem>> Update(int id, [FromBody] UpdateVoucherRequest request)
    {
        try
        {
            var voucher = await _voucherService.UpdateVoucherAsync(id, request);
            if (voucher == null)
                return NotFound(new MessageResponse("Voucher tidak ditemukan."));

            return Ok(voucher);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message!));
        }
    }

    // PATCH /api/v1/admin/vouchers/{id}/status — enable/disable
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateVoucherStatusRequest request)
    {
        var result = await _voucherService.UpdateVoucherStatusAsync(id, request);
        if (result == null)
            return NotFound(new MessageResponse("Voucher tidak ditemukan."));

        return Ok(new VoucherStatusResponse("Status voucher berhasil diperbarui.", result.Value));
    }

    // DELETE /api/v1/admin/vouchers/{id} — hapus (diblokir jika sudah dipakai)
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, errorMessage) = await _voucherService.DeleteVoucherAsync(id);

        if (!success)
        {
            // 404 if not found, 409 if has usage
            if (errorMessage == "Voucher tidak ditemukan.")
                return NotFound(new MessageResponse(errorMessage!));

            return Conflict(new MessageResponse(errorMessage!));
        }

        return Ok(new MessageResponse("Voucher berhasil dihapus."));
    }
}
