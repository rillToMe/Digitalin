using System.Text.Json;
using be_digitalin.Constants;
using be_digitalin.DTOs;

namespace be_digitalin.Services;

/// <summary>Satu entri registry: slug permanen (dipakai GameMetadata.Category), nama bisa di-rename admin.</summary>
public record GameCategoryEntry(string Slug, string Name);

/// <summary>
/// SSOT logika registry kategori storefront yang disimpan sebagai JSON di
/// AppSettings["GAME_CATEGORIES"]. Posisi di array = urutan tampil di homepage,
/// jadi reorder cukup menyusun ulang array (tanpa kolom sortOrder / tabel baru).
///
/// Semua fungsi di sini murni (tanpa DB) supaya bisa diuji langsung.
/// </summary>
public static class GameCategoryRegistry
{
    /// <summary>Batas jumlah kategori — tab homepage tidak lagi terbaca di atas ini.</summary>
    public const int MaxCategories = 30;

    /// <summary>Batas panjang nama — lebih dari ini merusak layout tab.</summary>
    public const int MaxNameLength = 40;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Registry bawaan (urutan sesuai AppConstants.StorefrontCategories.Defaults).</summary>
    public static List<GameCategoryEntry> Defaults() =>
        AppConstants.StorefrontCategories.Defaults
            .Select(category => new GameCategoryEntry(category.Slug, category.Name))
            .ToList();

    /// <summary>
    /// Baca registry dari JSON AppSettings. Null/kosong/korup → default, bukan
    /// exception: storefront tidak boleh mati gara-gara satu baris setting rusak.
    /// </summary>
    public static List<GameCategoryEntry> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Defaults();

        try
        {
            var entries = JsonSerializer
                .Deserialize<List<GameCategoryEntry>>(json, Options)?
                .Where(entry =>
                    !string.IsNullOrWhiteSpace(entry.Slug) &&
                    !string.IsNullOrWhiteSpace(entry.Name))
                .ToList();

            return entries is { Count: > 0 } ? entries : Defaults();
        }
        catch (JsonException)
        {
            return Defaults();
        }
    }

    public static string Serialize(IEnumerable<GameCategoryEntry> entries) =>
        JsonSerializer.Serialize(entries, Options);

    /// <summary>
    /// Validasi + normalisasi payload admin menjadi registry siap simpan.
    /// Urutan input dipertahankan — itulah urutan tampil.
    /// Melempar <see cref="InvalidOperationException"/> (dipetakan endpoint ke 400).
    /// </summary>
    public static List<GameCategoryEntry> Validate(
        IReadOnlyList<GameCategoryEntryRequest>? input)
    {
        if (input == null || input.Count == 0)
            throw new InvalidOperationException("Minimal satu kategori harus ada.");

        if (input.Count > MaxCategories)
            throw new InvalidOperationException(
                $"Maksimal {MaxCategories} kategori.");

        var result = new List<GameCategoryEntry>(input.Count);
        var seenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in input)
        {
            var name = item.Name?.Trim();

            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Nama kategori tidak boleh kosong.");

            if (name.Length > MaxNameLength)
                throw new InvalidOperationException(
                    $"Nama kategori \"{name}\" terlalu panjang (maksimal {MaxNameLength} karakter).");

            // Slug kiriman admin (kategori lama) menang; kosong = kategori baru → dari nama.
            var slug = ProductService.NormalizeCategory(item.Slug)
                       ?? ProductService.NormalizeCategory(name);

            if (slug == null)
                throw new InvalidOperationException(
                    $"Nama kategori \"{name}\" tidak bisa dijadikan slug.");

            if (AppConstants.StorefrontCategories.ReservedSlugs.Contains(slug))
                throw new InvalidOperationException(
                    $"Slug \"{slug}\" dipakai sistem dan tidak bisa dijadikan kategori.");

            if (!seenSlugs.Add(slug))
                throw new InvalidOperationException(
                    $"Slug kategori \"{slug}\" duplikat.");

            result.Add(new GameCategoryEntry(slug, name));
        }

        return result;
    }

    /// <summary>
    /// Gabungkan registry dengan jumlah brand per slug.
    /// <paramref name="includeEmpty"/>: false untuk storefront (kategori kosong tidak
    /// jadi section berjudul tanpa isi), true untuk admin (kategori baru harus
    /// terlihat walau belum ada game-nya).
    /// Slug yang ada di data tapi tidak ada di registry tetap ikut di akhir, supaya
    /// brand-nya tidak hilang dari homepage.
    /// </summary>
    public static List<GameCategoryInfo> Merge(
        IReadOnlyList<GameCategoryEntry> registry,
        IReadOnlyDictionary<string, int> brandCounts,
        bool includeEmpty)
    {
        var merged = registry
            .Select(entry => new GameCategoryInfo(
                entry.Slug,
                entry.Name,
                brandCounts.TryGetValue(entry.Slug, out var count) ? count : 0))
            .Where(info => includeEmpty || info.BrandCount > 0)
            .ToList();

        var known = registry
            .Select(entry => entry.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        merged.AddRange(brandCounts
            .Where(pair => !known.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new GameCategoryInfo(
                pair.Key,
                AppConstants.StorefrontCategories.Names.TryGetValue(pair.Key, out var name)
                    ? name
                    : pair.Key,
                pair.Value)));

        return merged;
    }
}
