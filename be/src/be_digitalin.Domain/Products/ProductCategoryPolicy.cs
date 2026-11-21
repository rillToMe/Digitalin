using be_digitalin.Constants;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Games.GameTemplates;

namespace be_digitalin.Domain.Products;

/// <summary>
/// Kebijakan domain untuk ekstensibilitas kategori produk digital (Games, Pulsa,
/// Paket Data, Voucher Digital, E-Money). Memisahkan asumsi spesifik game dari
/// katalog umum sambil menjaga kompatibilitas penuh dengan kategori default saat ini.
/// </summary>
public static class ProductCategoryPolicy
{
    public const string Games = AppConstants.Categories.Games;
    public const string Pulsa = AppConstants.Categories.Pulsa;
    public const string Data = "Data";
    public const string Voucher = "Voucher";
    public const string EMoney = "E-Money";

    private static readonly IReadOnlySet<string> SupportedSupplierCategories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Games,
            Pulsa,
            Data,
            Voucher,
            EMoney
        };

    /// <summary>
    /// Kategori supplier default yang disinkronkan secara otomatis (Games untuk menjaga
    /// perilaku existing 100% kompatibel).
    /// </summary>
    public static IReadOnlyList<string> DefaultSyncCategories { get; } = [Games];

    public static bool IsSupportedSupplierCategory(string? category) =>
        !string.IsNullOrWhiteSpace(category) &&
        SupportedSupplierCategories.Contains(category.Trim());

    /// <summary>
    /// Menentukan kategori storefront default untuk produk baru berdasarkan kategori supplier.
    /// </summary>
    public static string ResolveDefaultStorefrontCategory(string? supplierCategory)
    {
        if (string.IsNullOrWhiteSpace(supplierCategory))
            return AppConstants.StorefrontCategories.Defaults[0].Slug;

        var normalized = supplierCategory.Trim();
        if (string.Equals(normalized, Pulsa, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, Data, StringComparison.OrdinalIgnoreCase))
        {
            return "pulsa-data";
        }

        if (string.Equals(normalized, Voucher, StringComparison.OrdinalIgnoreCase))
        {
            return "voucher";
        }

        if (string.Equals(normalized, EMoney, StringComparison.OrdinalIgnoreCase))
        {
            return "e-money";
        }

        return AppConstants.StorefrontCategories.Defaults[0].Slug;
    }

    /// <summary>
    /// Menghasilkan skema input dan target template default berdasarkan kategori produk digital.
    /// Untuk Games menggunakan preset GameTemplateFactory; untuk Pulsa/Data/E-Money menggunakan
    /// input nomor tujuan/telepon.
    /// </summary>
    public static (List<GameInputField> InputFields, string TargetTemplate) CreateDefaultInputSchema(
        string? supplierCategory)
    {
        if (string.Equals(supplierCategory?.Trim(), Pulsa, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierCategory?.Trim(), Data, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierCategory?.Trim(), EMoney, StringComparison.OrdinalIgnoreCase))
        {
            const string phoneKey = "phoneNumber";
            return (
                [
                    new GameInputField
                    {
                        Key = phoneKey,
                        Label = "Nomor Tujuan",
                        Placeholder = "Masukkan nomor HP / tujuan",
                        Type = AppConstants.GameInputFieldTypes.Number,
                        Required = true,
                        Order = 1,
                        Options = []
                    }
                ],
                $"{{{phoneKey}}}");
        }

        return (
            GameTemplateFactory.CreateDefaultInputFields(),
            GameTemplateFactory.DefaultTargetTemplate);
    }
}
