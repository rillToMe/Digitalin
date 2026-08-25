namespace be_digitalin.Domain;

/// <summary>
/// Single Source of Truth harga jual katalog.
/// Harga modal Digiflazz + margin flat rupiah per produk
/// (GameProduct.CustomMargin); produk tanpa CustomMargin bermargin 0.
///
/// Semua penulis harga wajib lewat sini (sync add, sync update,
/// UpdateProductMarginAsync) supaya add dan update tidak pernah beda rumus.
/// </summary>
public static class Pricing
{
    /// <summary>Margin satu produk — tanpa CustomMargin berarti nol.</summary>
    public static decimal MarginOf(decimal? customMargin) =>
        customMargin ?? 0m;

    /// <summary>Harga jual dari harga modal supplier.</summary>
    public static decimal CalculateSellingPrice(
        decimal modalPrice,
        decimal? customMargin) =>
        modalPrice + MarginOf(customMargin);
}
