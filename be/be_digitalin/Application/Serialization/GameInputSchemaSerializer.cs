using System.Text.Json;
using be_digitalin.Domain.Entities;

namespace be_digitalin.Application.Serialization;

/// <summary>
/// Single Source of Truth untuk (de)serialisasi <see cref="GameInputField"/> ke/dari
/// kolom <c>GameMetadata.InputSchemaJson</c>.
///
/// Dipindah dari Infrastructure/Serialization (Fase 5): serialisasi JSON teknis
/// yang dipakai use-case (ProductService writer / OrderService reader) — bukan
/// business rule (Domain) dan bukan mekanisme infra. BCL-only, tanpa IO.
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