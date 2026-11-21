using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 5: migrasi POST /admin/auth/login dari Minimal API (AuthEndpoints.cs)
/// ke Controller. Endpoint anonymous + AdminLoginPolicy, persis seperti
/// .AllowAnonymous() + .RequireRateLimiting("AdminLoginPolicy") di endpoint lama.
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/admin/auth")]
[Tags("Otentikasi (Admin)")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService;

    public AdminAuthController(IAdminAuthService adminAuthService)
    {
        _adminAuthService = adminAuthService;
    }

    // POST /api/v1/admin/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AdminLoginPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token = await _adminAuthService.LoginAsync(
            request.Username,
            request.Password,
            request.CaptchaToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.RequestAborted);

        return token != null
            ? Ok(new TokenResponse(token))
            : Unauthorized();
    }
}
