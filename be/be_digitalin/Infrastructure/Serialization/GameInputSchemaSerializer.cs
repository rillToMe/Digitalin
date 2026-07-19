using System.Text.Json;
using be_digitalin.Models;

namespace be_digitalin.Infrastructure.Serialization;

/// <summary>
/// Single Source of Truth untuk (de)serialisasi <see cref="GameInputField"/> ke/dari
/// kolom <c>GameMetadata.InputSchemaJson</c>.
///
/// Sebelumnya logika ini terduplikasi di ProductService (writer, camelCase) dan
/// OrderService (reader, case-insensitive). Kini seluruh backend memakai satu
/// konfigurasi JSON yang sama sehingga tidak mungkin drift.
///
/// Opsi read sengaja menggabungkan camelCase policy + case-insensitive agar
/// kompatibel dengan seluruh data lama maupun baru.
/// </summary>
public static class GameInputSchemaSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Serialisasi daftar input field ke JSON (camelCase).</summary>
    public static string Serialize(IReadOnlyList<GameInputField> inputFields) =>
        JsonSerializer.Serialize(inputFields, Options);

    /// <summary>
    /// Deserialisasi JSON schema menjadi daftar input field.
    /// Mengembalikan list kosong bila JSON null/kosong.
    /// Melempar <see cref="InvalidOperationException"/> bila JSON korup.
    /// </summary>
    public static List<GameInputField> Deserialize(string? inputSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(inputSchemaJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<GameInputField>>(inputSchemaJson, Options) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Konfigurasi schema input game tidak valid.",
                ex);
        }
    }
}
