namespace be_digitalin.Domain.Entities;

public class GameMetadata
{
    public int Id { get; set; }

    public string Brand { get; set; } = string.Empty;

    /// <summary>
    /// Slug brand untuk URL detail game, dipersist saat sync/simpan lewat
    /// Domain.Slug.From — bukan dihitung ulang di query. Satu-satunya sumber
    /// slug brand: FE memakainya apa adanya, endpoint detail mencari kolom ini.
    /// </summary>
    public string BrandSlug { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string InputSchemaJson { get; set; } = "[]";

    public string TargetTemplate { get; set; } = "{userId}";

    /// <summary>
    /// Produk Digiflazz khusus validasi nama akun. Produk ini tidak dijual dan
    /// tidak dikirim dalam katalog publik.
    /// </summary>
    public int? UsernameCheckProductId { get; set; }

    /// <summary>
    /// Instruksi supplier (field "desc" Digiflazz), disimpan sekali per brand —
    /// bukan per produk, karena isinya sama untuk seluruh SKU satu game.
    /// Hanya informatif untuk admin; TargetTemplate + InputSchemaJson tetap
    /// satu-satunya sumber pembentuk customer_no.
    /// </summary>
    public string? SupplierNote { get; set; }

    /// <summary>
    /// Slug kategori storefront yang dimiliki game, disimpan CSV ("mobile,pc").
    /// Satu game boleh masuk beberapa kategori. Null/kosong = belum dikategorikan.
    ///
    /// Aman sebagai CSV karena slug dinormalisasi ke [a-z0-9-] oleh
    /// Domain.Slug — koma tidak mungkin lolos ke dalam slug.
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
