namespace be_digitalin.DTOs;

public record CategoryInfo(
    string Brand,
    string Slug,
    string? ImageUrl,
    string Category,
    // Slug kategori storefront yang dimiliki brand; kosong = belum
    // dikategorikan (FE mengelompokkannya ke "Lainnya").
    IReadOnlyList<string> CategorySlugs
);
