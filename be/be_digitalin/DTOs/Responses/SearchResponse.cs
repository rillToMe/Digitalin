namespace be_digitalin.DTOs;

public record GameSearchResult(
    int Id,
    string Brand,
    string Slug,
    string? ImageUrl,
    string InputType
);
