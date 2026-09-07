using System.Security.Claims;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.Providers;
using be_digitalin.Services.Auth;
using be_digitalin.Services.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace be_digitalin.Controllers;

/// <summary>
/// Fase 5: migrasi alur auth customer (/auth, 9 endpoint) dari Minimal API
/// (AuthEndpoints.cs) ke Controller. Rate limit & reCAPTCHA dipertahankan
/// per-endpoint via [EnableRateLimiting]; manual Validator.TryValidateObject
/// diganti auto-validation [ApiController] (bentuk error { message } dijaga
/// shim Fase 0 — sama seperti legacy).
/// </summary>
[ApiController]
[Route(AppConstants.Api.Prefix + "/auth")]
[Tags("Otentikasi (Customer)")]
public class AuthController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IFacebookAuthService _facebookAuthService;
    private readonly IOptions<FacebookAuthSettings> _facebookAuthSettings;

    public AuthController(
        ICustomerService customerService,
        IPasswordResetService passwordResetService,
        IGoogleAuthService googleAuthService,
        IFacebookAuthService facebookAuthService,
        IOptions<FacebookAuthSettings> facebookAuthSettings)
    {
        _customerService = customerService;
        _passwordResetService = passwordResetService;
        _googleAuthService = googleAuthService;
        _facebookAuthService = facebookAuthService;
        _facebookAuthSettings = facebookAuthSettings;
    }

    private string? RemoteIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    // POST /api/v1/auth/local/request-otp
    [HttpPost("local/request-otp")]
    [EnableRateLimiting("OtpPolicy")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest req)
    {
        var result = await _customerService.RequestRegistrationOtpAsync(
            req.Email,
            req.CaptchaToken,
            RemoteIp,
            HttpContext.RequestAborted);

        return result.Success
            ? Ok(new MessageResponse(result.Message))
            : BadRequest(new MessageResponse(result.Message));
    }

    // POST /api/v1/auth/local/register
    [HttpPost("local/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var token = await _customerService.RegisterAsync(
            req.Email,
            req.Password,
            req.FullName,
            req.OtpCode,
            HttpContext.RequestAborted);

        return token != null
            ? Ok(new TokenResponse(token))
            : BadRequest(new MessageResponse("Kode OTP tidak valid atau email sudah terdaftar."));
    }

    // POST /api/v1/auth/local/login
    [HttpPost("local/login")]
    [EnableRateLimiting("CustomerLoginPolicy")]
    public async Task<IActionResult> Login([FromBody] CustomerLoginRequest req)
    {
        var token = await _customerService.LoginAsync(
            req.Email,
            req.Password,
            req.CaptchaToken,
            RemoteIp,
            HttpContext.RequestAborted);

        return token != null
            ? Ok(new TokenResponse(token))
            : Unauthorized();
    }

    // POST /api/v1/auth/local/request-password-reset
    // [ApiController] auto-validate RequestPasswordResetRequest (Email wajib,
    // format email) → 400 { message } via shim — pengganti manual
    // Validator.TryValidateObject di endpoint lama.
    [HttpPost("local/request-password-reset")]
    [EnableRateLimiting("OtpPolicy")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest req)
    {
        var success = await _passwordResetService.RequestPasswordResetAsync(req.Email, HttpContext.RequestAborted);

        if (!success)
        {
            return BadRequest(new MessageResponse("Email tidak terdaftar."));
        }

        return Ok(new MessageResponse("Kode OTP reset password berhasil dikirim."));
    }

    // POST /api/v1/auth/local/reset-password
    [HttpPost("local/reset-password")]
    [EnableRateLimiting("OtpPolicy")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var result = await _passwordResetService.ResetPasswordAsync(
            req.Email,
            req.OtpCode,
            req.NewPassword,
            HttpContext.RequestAborted);

        return result switch
        {
            PasswordResetResult.Success       => Ok(new MessageResponse("Password berhasil diubah.")),
            PasswordResetResult.EmailNotFound => BadRequest(new MessageResponse("Email tidak terdaftar.")),
            PasswordResetResult.InvalidOtp    => BadRequest(new MessageResponse("Kode OTP salah atau sudah kedaluwarsa.")),
            _                                 => Problem("Terjadi kesalahan yang tidak diketahui.")
        };
    }

    // POST /api/v1/auth/google — verifikasi Google ID token, auto-register jika perlu
    [HttpPost("google")]
    [EnableRateLimiting("CustomerLoginPolicy")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.IdToken))
        {
            return BadRequest(new MessageResponse("ID token tidak boleh kosong."));
        }

        try
        {
            var result = await _googleAuthService.AuthenticateWithGoogleAsync(req.IdToken);
            if (result == null)
            {
                return Unauthorized();
            }

            return Ok(new TokenResponse(result.Token));
        }
        catch (GoogleAuthenticationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    // GET /api/v1/auth/facebook/login — mulai OAuth Facebook: terbitkan state
    // CSRF lalu redirect browser ke dialog otorisasi Facebook (full-page
    // navigation, bukan XHR — browser yang menangani seluruh roundtrip).
    [HttpGet("facebook/login")]
    [EnableRateLimiting("CustomerLoginPolicy")]
    public async Task<IActionResult> FacebookLogin()
    {
        try
        {
            var authorizationUrl = await _facebookAuthService.BeginLoginAsync();
            return Redirect(authorizationUrl);
        }
        catch (FacebookAuthenticationException ex)
        {
            return FacebookErrorRedirect(ex.Message);
        }
    }

    // GET /api/v1/auth/facebook/callback — Facebook mengarahkan balang ke sini
    // dengan code+state. Service memvalidasi state (CSRF), menukar code,
    // login/auto-register akun Facebook, lalu browser di-redirect ke frontend
    // membawa JWT di URL fragment (fragment tidak pernah dikirim ke server —
    // token tidak bocor ke log/referrer).
    [HttpGet("facebook/callback")]
    [EnableRateLimiting("CustomerLoginPolicy")]
    public async Task<IActionResult> FacebookCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var result = await _facebookAuthService.CompleteLoginAsync(
            code, state, error, cancellationToken);

        if (result.Success)
        {
            return Redirect(
                $"{_facebookAuthSettings.Value.FrontendRedirectUri}" +
                $"#token={Uri.EscapeDataString(result.Token!)}");
        }

        return FacebookErrorRedirect(result.Error ?? "Login Facebook gagal.");
    }

    /// <summary>Arahkan error ke halaman callback frontend via fragment #error=.</summary>
    private IActionResult FacebookErrorRedirect(string message)
    {
        var frontend = _facebookAuthSettings.Value.FrontendRedirectUri;

        if (string.IsNullOrWhiteSpace(frontend))
        {
            // Frontend URI belum dikonfigurasi — redirect tidak mungkin;
            // kembalikan bentuk { message } (kontrak error existing).
            return BadRequest(new MessageResponse(message));
        }

        return Redirect($"{frontend}#error={Uri.EscapeDataString(message)}");
    }

    // GET /api/v1/auth/profile
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null) return Unauthorized();

        var customer = await _customerService.GetProfileAsync(int.Parse(idClaim.Value));
        if (customer == null) return NotFound();

        return Ok(new ProfileResponse(
            customer.Email,
            customer.FullName,
            customer.CreatedAt,
            customer.AvatarUrl));
    }

    // PUT /api/v1/auth/profile
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null) return Unauthorized();

        var success = await _customerService.UpdateProfileAsync(int.Parse(idClaim.Value), req.FullName, req.NewPassword);

        return success
            ? Ok(new MessageResponse("Profil berhasil diperbarui!"))
            : NotFound();
    }

    // GET /api/v1/auth/check-email?email=...
    [HttpGet("check-email")]
    public async Task<IActionResult> CheckEmail([FromQuery] string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new MessageResponse("Email tidak boleh kosong."));
        }

        var emailSudahTerpakai = await _customerService.CheckEmailExistsAsync(email);

        if (emailSudahTerpakai)
        {
            return BadRequest(new MessageResponse("Email sudah terdaftar. Silakan login."));
        }
        return Ok(new MessageResponse("Email tersedia."));
    }
}
