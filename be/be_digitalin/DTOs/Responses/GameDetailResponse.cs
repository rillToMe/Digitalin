using be_digitalin.Models;

namespace be_digitalin.DTOs;

public record GameDetailResponse(
    string Brand,
    string GameCode,
    string? ImageUrl,
    IReadOnlyList<GameInputField> InputFields,
    IReadOnlyList<GameProductItemResponse> Products
);

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