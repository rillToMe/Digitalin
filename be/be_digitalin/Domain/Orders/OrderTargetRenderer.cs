using be_digitalin.Constants;
using be_digitalin.Domain.Entities;

namespace be_digitalin.Domain.Orders;

/// <summary>
/// Aturan murni pembentukan customer_no Digiflazz dari input field game
/// (Fase 5 — diekstrak dari OrderService). Tanpa IO, tanpa EF, tanpa provider.
/// Orchestrasi (deserialisasi schema + urutan validasi/render) tetap di
/// OrderService (Application): class ini hanya berisi aturan validaasi &
/// rendering yang bisa diuji langsung.
/// </summary>
public static class OrderTargetRenderer
{
    /// <summary>
    /// Validasi nilai input per konfigurasi field (required, tipe number,
    /// pilihan select). Melempar InvalidOperationException dengan pesan user-facing.
    /// </summary>
    public static void ValidateTargetValues(
        IReadOnlyList<GameInputField> inputFields,
        Dictionary<string, string?> targetValues)
    {
        if (inputFields.Count == 0)
        {
            throw new InvalidOperationException(
                "Game belum memiliki konfigurasi input.");
        }

        foreach (var field in inputFields)
        {
            targetValues.TryGetValue(
                field.Key,
                out var rawValue);

            var value = rawValue?.Trim();

            if (field.Required &&
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{field.Label} wajib diisi.");
            }

            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (field.Type == AppConstants.GameInputFieldTypes.Number &&
                value.Any(character => !char.IsDigit(character)))
            {
                throw new InvalidOperationException(
                    $"{field.Label} hanya boleh berisi angka.");
            }

            if (field.Type == AppConstants.GameInputFieldTypes.Select)
            {
                var isValidOption = field.Options.Any(option =>
                    string.Equals(
                        option,
                        value,
                        StringComparison.OrdinalIgnoreCase));

                if (!isValidOption)
                {
                    throw new InvalidOperationException(
                        $"Pilihan {field.Label} tidak valid.");
                }
            }
        }
    }

    /// <summary>
    /// Render template tujuan (mis. "ID:{userId}|Zone:{serverId}") dengan
    /// substitusi nilai per key field. Melempar InvalidOperationException bila
    /// template kosong, menyisakan placeholder, atau hasil akhir kosong.
    /// </summary>
    public static string RenderTargetTemplate(
        string targetTemplate,
        IReadOnlyList<GameInputField> inputFields,
        Dictionary<string, string?> targetValues)
    {
        if (string.IsNullOrWhiteSpace(targetTemplate))
        {
            throw new InvalidOperationException(
                "Template tujuan game belum dikonfigurasi.");
        }

        var result = targetTemplate;

        foreach (var field in inputFields)
        {
            targetValues.TryGetValue(
                field.Key,
                out var rawValue);

            var value = rawValue?.Trim() ?? string.Empty;

            result = result.Replace(
                $"{{{field.Key}}}",
                value,
                StringComparison.OrdinalIgnoreCase);
        }

        if (result.Contains('{') || result.Contains('}'))
        {
            throw new InvalidOperationException(
                "Template tujuan game memiliki field yang tidak valid.");
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                "Data tujuan tidak valid.");
        }

        return result;
    }
}