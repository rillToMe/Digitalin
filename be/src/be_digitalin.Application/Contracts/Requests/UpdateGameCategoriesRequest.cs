namespace be_digitalin.DTOs;

/// <summary>
/// Ganti seluruh registry kategori storefront sekali jalan (reorder + rename +
/// tambah + hapus). Urutan array = urutan section/tab di homepage.
/// </summary>
public record UpdateGameCategoriesRequest(
    IReadOnlyList<GameCategoryEntryRequest> Categories
);

/// <summary>
/// Satu baris kategori dari form admin.
/// <paramref name="Slug"/> wajib dikirim untuk kategori yang sudah ada supaya
/// rename nama tidak memutus GameMetadata.Category; null/kosong = kategori baru,
/// slug diturunkan dari <paramref name="Name"/>.
/// </summary>
public record GameCategoryEntryRequest(
    string? Slug,
    string Name
);
