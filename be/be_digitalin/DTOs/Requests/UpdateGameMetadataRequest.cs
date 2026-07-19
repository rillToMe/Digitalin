using be_digitalin.Models;

namespace be_digitalin.DTOs;

public record UpdateGameMetadataRequest(
    IReadOnlyList<GameInputField> InputFields,
    string TargetTemplate
);