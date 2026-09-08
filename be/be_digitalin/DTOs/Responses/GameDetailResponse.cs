using be_digitalin.Domain.Entities;

namespace be_digitalin.DTOs;

public record GameDetailResponse(
    string Brand,
    string GameCode,
    string? ImageUrl,
    IReadOnlyList<GameInputField> InputFields,
    bool CanCheckUsername,
    IReadOnlyList<GameProductItemResponse> Products
);

public record UsernameCheckResponse(bool IsValid, string? Username, string Message);

public record GameProductItemResponse(
    int Id,
    string Name,
    decimal Price,
    string GameCode,
    string Brand,
    string Category,
    string? ImageUrl,
    decimal? CustomMargin
);
