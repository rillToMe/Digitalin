using be_digitalin.Models;

namespace be_digitalin.DTOs;

public record UpdateGameMetadataRequest(
    IReadOnlyList<GameInputField> InputFields,
    string TargetTemplate,
    // Kategori storefront game, boleh lebih dari satu. Tiap nama/slug
    // dinormalisasi BE; null/kosong = belum dikategorikan.
    IReadOnlyList<string>? Categories
);
