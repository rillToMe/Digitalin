using System.ComponentModel.DataAnnotations;

namespace be_digitalin.DTOs.Requests;

/// <summary>Request DTO for POST /auth/local/request-password-reset</summary>
public record RequestPasswordResetRequest(
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    string Email
);

/// <summary>Request DTO for POST /auth/local/reset-password</summary>
public record ResetPasswordRequest(
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    string Email,

    [Required(ErrorMessage = "Kode OTP wajib diisi.")]
    string OtpCode,

    [Required(ErrorMessage = "Password baru wajib diisi.")]
    [MinLength(8, ErrorMessage = "Password baru minimal 8 karakter.")]
    string NewPassword
);
