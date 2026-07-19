using be_digitalin.Models;

namespace be_digitalin.DTOs;

public record GameMetadataResponse(
    int Id,
    string Brand,
    string? ImageUrl,
    IReadOnlyList<GameInputField> InputFields,
    string TargetTemplate
);