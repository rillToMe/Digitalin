using System.ComponentModel.DataAnnotations;

namespace be_digitalin.Config;

/// <summary>
/// Kunci HMAC untuk PublicHash (URL status publik order). Sumber:
/// konfigurasi/environment `OrderPublicHash__SecretKey` — tidak pernah
/// di-hardcode di kode. Rotasi kunci membatalkan semua PublicHash yang
/// tersimpan (jalankan ulang backfill bila dirotasi).
/// </summary>
public class OrderPublicHashSettings
{
    [Required]
    [MinLength(32, ErrorMessage = "OrderPublicHash:SecretKey minimal 32 karakter (HMAC-SHA256, >= 256 bit).")]
    public string SecretKey { get; set; } = string.Empty;
}
