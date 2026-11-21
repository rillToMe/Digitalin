namespace be_digitalin.DTOs;

/// <summary>
/// FAQ publik/admin — field public-safe saja, entity EF tidak pernah
/// terekspos langsung ke kontrak API.
/// </summary>
public record FaqResponse(
    int Id,
    string Question,
    string Answer,
    string Category,
    bool Featured);
