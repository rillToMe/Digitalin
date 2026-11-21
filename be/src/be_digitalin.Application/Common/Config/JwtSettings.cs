using System.ComponentModel.DataAnnotations;

namespace be_digitalin.Config;

public class JwtSettings
{
    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32, ErrorMessage = "Jwt:Key minimal 32 karakter (HS256 butuh kunci >= 256 bit).")]
    public string Key { get; set; } = string.Empty;
}