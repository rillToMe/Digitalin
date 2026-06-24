using System.ComponentModel.DataAnnotations;

namespace be_digitalin.DTOs.Requests;

/// <summary>Request DTO for PUT /admin/settings/maintenance</summary>
public record UpdateMaintenanceRequest(
    [Required]
    bool Enabled,

    [Required(ErrorMessage = "Title wajib diisi.")]
    [MaxLength(200, ErrorMessage = "Title maksimal 200 karakter.")]
    string Title,

    [Required(ErrorMessage = "Message wajib diisi.")]
    [MaxLength(1000, ErrorMessage = "Message maksimal 1000 karakter.")]
    string Message,

    /// <summary>ISO 8601 datetime string or null/empty if unknown.</summary>
    string? EstimatedFinish,

    [Required]
    bool AllowAdmin
);
