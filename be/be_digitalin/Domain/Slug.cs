using System.Text.RegularExpressions;

namespace be_digitalin.Domain;

/// <summary>
/// Single Source of Truth slug: satu-satunya tempat teks bebas diubah menjadi
/// kebab-case aman URL. Dipakai slug kategori storefront dan
/// <see cref="Models.GameMetadata.BrandSlug"/>, supaya tidak ada dua aturan slug
/// yang bisa saling menyimpang.
/// </summary>
public static class Slug
{
    /// <summary>Apa pun yang bukan huruf/angka jadi pemisah slug.</summary>
    private static readonly Regex NonSlugCharacters =
        new("[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Teks bebas → slug kebab-case: lowercase, karakter non-alfanumerik jadi
    /// hyphen, hyphen di ujung dibuang ("Rainbow Six Mobile (Indonesia)" →
    /// "rainbow-six-mobile-indonesia"). Null bila tidak ada huruf/angka sama sekali.
    /// </summary>
    public static string? From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var slug = NonSlugCharacters
            .Replace(text.ToLowerInvariant(), "-")
            .Trim('-');

        return slug.Length == 0 ? null : slug;
    }
}
