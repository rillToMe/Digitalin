namespace be_digitalin.DTOs.Requests;

// SSOT request DTO area otentikasi — sebelumnya didefinisikan inline di
// Endpoints/AuthEndpoints.cs (men-shading versi lama di namespace DTOs).
// Semua dipindah ke sini bersamaan migrasi Minimal API → Controllers.

public record LoginRequest(
    string Username,
    string Password,
    string CaptchaToken);

public record CustomerLoginRequest(
    string Email,
    string Password,
    string CaptchaToken);

public record RequestOtpRequest(
    string Email,
    string CaptchaToken);

public record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string OtpCode);

public record UpdateProfileRequest(
    string FullName,
    string? NewPassword);

public record GoogleLoginRequest(string IdToken);