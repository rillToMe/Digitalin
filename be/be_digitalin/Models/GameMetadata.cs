namespace be_digitalin.Models;

public class GameMetadata
{
    public int Id { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string InputSchemaJson { get; set; } = "[]";

    public string TargetTemplate { get; set; } = "{userId}";

    /// <summary>
    /// Slug kategori storefront yang dimiliki game, disimpan CSV ("mobile,pc").
    /// Satu game boleh masuk beberapa kategori. Null/kosong = belum dikategorikan.
    ///
    /// Aman sebagai CSV karena slug dinormalisasi ke [a-z0-9-] oleh
    /// ProductService.NormalizeCategory — koma tidak mungkin lolos ke dalam slug.
    ///
    /// ponytail: CSV di satu kolom, bukan tabel join. Tidak ada query "game per
    /// kategori" di SQL — pengelompokan dilakukan di memori (katalog ratusan baris).
    /// Pindah ke tabel join kalau nanti butuh filter/paging per kategori di DB.
    /// </summary>
    public string? Categories { get; set; }

    /// <summary>CSV → daftar slug; daftar kosong bila belum dikategorikan.</summary>
    public static List<string> ParseCategories(string? csv) =>
        csv?.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? [];

    /// <summary>Daftar slug → CSV untuk disimpan; daftar kosong → null.</summary>
    public static string? JoinCategories(IEnumerable<string> slugs)
    {
        var csv = string.Join(",", slugs);
        return csv.Length == 0 ? null : csv;
    }
}
