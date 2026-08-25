using be_digitalin.Models;

namespace be_digitalin.Services.Games.GameTemplates;

/// <summary>
/// Preset lengkap sebuah game: daftar InputFields default beserta
/// TargetTemplate yang menghasilkan customer_no untuk Digiflazz.
/// Immutable — dibangun sekali oleh <see cref="GameTemplateFactory"/>.
/// </summary>
public sealed class GameTemplate
{
    public required GameTemplateType Type { get; init; }

    /// <summary>Label yang bisa ditampilkan admin (mis. "User ID + Server").</summary>
    public required string DisplayName { get; init; }

    public required IReadOnlyList<GameInputField> InputFields { get; init; }

    public required string TargetTemplate { get; init; }
}
