namespace be_digitalin.DTOs;

public record GameSearchResult(
    int Id,
    string Brand,
    string Slug,
    string? ImageUrl,
    // Slug kategori storefront; kosong = belum dikategorikan.
    IReadOnlyList<string> CategorySlugs
);
