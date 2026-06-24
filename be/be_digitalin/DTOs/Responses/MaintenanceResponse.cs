namespace be_digitalin.DTOs.Responses;

/// <summary>
/// Response DTO for GET /admin/settings/maintenance
/// and the 503 body sent to blocked users.
/// </summary>
public record MaintenanceStatusResponse(
    bool    Enabled,
    string  Title,
    string  Message,
    string? EstimatedFinish,
    bool    AllowAdmin
);

/// <summary>
/// HTTP 503 body returned by MaintenanceMiddleware when maintenance is active.
/// </summary>
public record MaintenanceBlockedResponse(
    bool    Maintenance,
    string  Title,
    string  Message,
    string? EstimatedFinish
);
