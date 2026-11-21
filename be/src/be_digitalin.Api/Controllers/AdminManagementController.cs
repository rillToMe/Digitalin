using System.Security.Claims;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 8: migrasi /admin/admins (super-admin, 5 endpoint) dari Minimal API
/// (AdminManagementEndpoints.cs) ke Controller — domain terakhir yang pindah.
/// Otorisasi SuperAdminOnly di level class; caller username (untuk proteksi
/// self-delete / demote diri sendiri) dibaca dari klaim Name.
/// </summary>
[ApiController]
[Authorize(Policy = "SuperAdminOnly")]
[Route(AppConstants.Api.Prefix + "/admin/admins")]
[Tags("Admin Management")]
public class AdminManagementController : ControllerBase
{
    private readonly IAdminManagementService _service;

    public AdminManagementController(IAdminManagementService service)
    {
        _service = service;
    }

    private string? CallerUsername => User.FindFirst(ClaimTypes.Name)?.Value;

    // GET /api/v1/admin/admins
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminListItem>>> GetAdmins()
    {
        var admins = await _service.GetAdminsAsync();
        return Ok(admins);
    }

    // POST /api/v1/admin/admins — buat admin baru
    [HttpPost]
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest req)
    {
        var result = await _service.CreateAdminAsync(req);
        return MapAdminActionResult(result);
    }

    // PUT /api/v1/admin/admins/{id}/role
    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateAdminRoleRequest req)
    {
        var result = await _service.UpdateAdminRoleAsync(id, req.Role, CallerUsername);
        return MapAdminActionResult(result);
    }

    // PUT /api/v1/admin/admins/{id}/password
    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetAdminPasswordRequest req)
    {
        var result = await _service.ResetAdminPasswordAsync(id, req.NewPassword);
        return MapAdminActionResult(result);
    }

    // DELETE /api/v1/admin/admins/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAdmin(int id)
    {
        var result = await _service.DeleteAdminAsync(id, CallerUsername);
        return MapAdminActionResult(result);
    }

    // ── Helpers ─────────────────────────────────────────────────
    private static IActionResult MapAdminActionResult(AdminActionResult result) =>
        result.Outcome switch
        {
            AdminActionOutcome.Success  => new OkObjectResult(new MessageResponse(result.Message)),
            AdminActionOutcome.NotFound => new NotFoundObjectResult(new MessageResponse(result.Message)),
            _                           => new BadRequestObjectResult(new MessageResponse(result.Message)),
        };
}
