using System.Globalization;

namespace be_digitalin.Domain;

/// <summary>
/// Single Source of Truth harga jual katalog dan pembulatan nominal Rupiah (IDR).
/// Harga modal Digiflazz + margin flat rupiah per produk
/// (GameProduct.CustomMargin); produk tanpa CustomMargin bermargin 0.
///
/// Semua penulis harga wajib lewat sini (sync add, sync update,
/// UpdateProductMarginAsync) supaya add dan update tidak pernah beda rumus.
/// Seluruh perhitungan harga, diskon voucher, total tagihan order, payload
/// Midtrans Core API, dan verifikasi webhook wajib menggunakan pembulatan
/// bilangan bulat Rupiah yang konsisten.
/// </summary>
public static class Pricing
{
    /// <summary>
    /// Bulatkan nilai desimal ke bilangan bulat Rupiah terdekat
    /// (<see cref="MidpointRounding.AwayFromZero"/>).
    /// </summary>
    public static decimal RoundRupiah(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Konversi nilai Rupiah ke integer bulat positif untuk payload Midtrans Core API.
    /// </summary>
    public static int ToRupiahInt(decimal value)
    {
        var rounded = RoundRupiah(value);
        if (rounded <= 0m || rounded > int.MaxValue)
        {
            throw new InvalidOperationException("Nominal pembayaran Rupiah harus lebih besar dari 0.");
        }

        return decimal.ToInt32(rounded);
    }

    /// <summary>
    /// Parse string nominal Midtrans (mis. "15199.00" atau "15199") menjadi bilangan bulat Rupiah.
    /// Menolak nilai kosong, negatif/nol, atau nilai yang memiliki pecahan sen non-nol.
    /// </summary>
    public static bool TryParseWholeRupiah(string? rawAmount, out decimal wholeRupiah)
    {
        wholeRupiah = 0m;
        if (string.IsNullOrWhiteSpace(rawAmount))
            return false;

        if (!decimal.TryParse(
                rawAmount.Trim(),
                NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return false;
        }

        if (parsed <= 0m || parsed != decimal.Truncate(parsed))
            return false;

        wholeRupiah = decimal.Truncate(parsed);
        return true;
    }

    /// <summary>
    /// Verifikasi apakah string gross_amount dari Midtrans sama persis dengan nominal Rupiah bulat yang diharapkan.
    /// </summary>
    public static bool IsExactRupiahMatch(string? rawAmount, decimal expectedAmount)
    {
        var expectedRounded = RoundRupiah(expectedAmount);
        if (expectedRounded <= 0m || expectedAmount != expectedRounded)
            return false;

        return TryParseWholeRupiah(rawAmount, out var parsedWholeRupiah)
            && parsedWholeRupiah == expectedRounded;
    }

    /// <summary>Margin satu produk — tanpa CustomMargin berarti nol.</summary>
    public static decimal MarginOf(decimal? customMargin) =>
        customMargin ?? 0m;

    /// <summary>Harga jual dari harga modal supplier (dibulatkan ke Rupiah bulat).</summary>
    public static decimal CalculateSellingPrice(
        decimal modalPrice,
        decimal? customMargin) =>
        RoundRupiah(modalPrice + MarginOf(customMargin));
}
