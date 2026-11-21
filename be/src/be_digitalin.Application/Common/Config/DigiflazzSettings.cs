using System.ComponentModel.DataAnnotations;

namespace be_digitalin.Config;

public class DigiflazzSettings
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string WebhookSecret { get; set; } = string.Empty;
}