using System.ComponentModel.DataAnnotations;

namespace be_digitalin.Config;

/// <summary>
/// Konfigurasi OAuth Facebook Login. Section: "FacebookAuth" (appsettings /
/// env var Facebook__*). Semua nilai per-environment — jangan hardcode.
/// AppSecret HANYA via environment variable / secret management, bukan source.
/// </summary>
public class FacebookAuthSettings
{
    [Required]
    public string AppId { get; set; } = string.Empty;

    [Required]
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>Callback OAuth di backend, harus identik dengan yang terdaftar
    /// di Meta App (contoh dev: http://localhost:5210/api/v1/auth/facebook/callback).</summary>
    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>URL frontend tujuan redirect setelah callback selesai
    /// (contoh dev: http://localhost:5173/auth/facebook/callback).</summary>
    [Required]
    public string FrontendRedirectUri { get; set; } = string.Empty;
}
