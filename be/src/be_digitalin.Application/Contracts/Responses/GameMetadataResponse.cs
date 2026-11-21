using be_digitalin.Domain.Entities;

namespace be_digitalin.DTOs;

public record GameMetadataResponse(
    int Id,
    string Brand,
    // Slug brand tersimpan — dipakai FE sebagai identitas game di URL/pilihan
    // populer, tidak boleh dihitung ulang di sisi FE.
    string BrandSlug,
    string? ImageUrl,
    IReadOnlyList<GameInputField> InputFields,
    string TargetTemplate,
    // Instruksi supplier Digiflazz (read-only, informatif untuk admin).
    string? SupplierNote,
    // Slug kategori storefront; kosong = belum dikategorikan.
    IReadOnlyList<string> Categories,
    int? UsernameCheckProductId
);
