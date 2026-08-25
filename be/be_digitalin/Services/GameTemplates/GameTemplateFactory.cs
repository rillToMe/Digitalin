using be_digitalin.Constants;
using be_digitalin.Models;

namespace be_digitalin.Services.GameTemplates;

/// <summary>
/// Single Source of Truth untuk seluruh preset template game.
/// Menghasilkan InputFields (lengkap dengan placeholder, required, type, order)
/// beserta TargetTemplate yang dipakai OrderService.BuildTargetValue()
/// untuk membentuk customer_no Digiflazz.
///
/// Menambah game baru cukup dengan menambah entry di sini —
/// tanpa menyentuh business logic di ProductService / OrderService.
/// </summary>
public static class GameTemplateFactory
{
    // Registry preset — diinisialisasi sekali, immutable.
    private static readonly IReadOnlyDictionary<GameTemplateType, GameTemplate> Templates =
        BuildTemplates();

    /// <summary>Preset default saat metadata baru dibuat lewat sync Digiflazz.</summary>
    public const GameTemplateType DefaultType = GameTemplateType.UserId;

    /// <summary>Seluruh preset yang tersedia (untuk endpoint / admin UI).</summary>
    public static IReadOnlyList<GameTemplate> All => Templates.Values.ToList();

    public static GameTemplate Get(GameTemplateType type) => Templates[type];

    /// <summary>InputFields default untuk metadata baru (preset default).</summary>
    public static List<GameInputField> CreateDefaultInputFields() =>
        CloneFields(Templates[DefaultType].InputFields);

    /// <summary>TargetTemplate default untuk metadata baru (preset default).</summary>
    public static string DefaultTargetTemplate => Templates[DefaultType].TargetTemplate;

    private static IReadOnlyDictionary<GameTemplateType, GameTemplate> BuildTemplates()
    {
        var list = new List<GameTemplate>
        {
            new()
            {
                Type = GameTemplateType.UserId,
                DisplayName = "User ID",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.UserId}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.UserId, "User ID", "Masukkan User ID", AppConstants.GameInputFieldTypes.Number, order: 1)
                ]
            },
            new()
            {
                Type = GameTemplateType.UserIdServer,
                DisplayName = "User ID + Server",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.UserId}}}{{{AppConstants.GameFieldKeys.Server}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.UserId, "User ID", "Masukkan User ID", AppConstants.GameInputFieldTypes.Number, order: 1),
                    Field(AppConstants.GameFieldKeys.Server, "Server", "Masukkan Server", AppConstants.GameInputFieldTypes.Number, order: 2)
                ]
            },
            new()
            {
                Type = GameTemplateType.UserIdServerPipe,
                DisplayName = "User ID | Server",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.UserId}}}|{{{AppConstants.GameFieldKeys.Server}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.UserId, "User ID", "Masukkan User ID", AppConstants.GameInputFieldTypes.Number, order: 1),
                    // Server Digiflazz berupa nama region (os_asia, os_usa, os_euro),
                    // jadi text — bukan Number seperti preset UserIdServer.
                    Field(AppConstants.GameFieldKeys.Server, "Server", "Masukkan Server (mis. os_asia)", AppConstants.GameInputFieldTypes.Text, order: 2)
                ]
            },
            new()
            {
                Type = GameTemplateType.UserIdZone,
                DisplayName = "User ID + Zone ID",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.UserId}}}{{{AppConstants.GameFieldKeys.Zone}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.UserId, "User ID", "Masukkan User ID", AppConstants.GameInputFieldTypes.Number, order: 1),
                    Field(AppConstants.GameFieldKeys.Zone, "Zone ID", "Masukkan Zone ID", AppConstants.GameInputFieldTypes.Number, order: 2)
                ]
            },
            new()
            {
                Type = GameTemplateType.PlayerId,
                DisplayName = "Player ID",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.PlayerId}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.PlayerId, "Player ID", "Masukkan Player ID", AppConstants.GameInputFieldTypes.Number, order: 1)
                ]
            },
            new()
            {
                Type = GameTemplateType.PlayerIdServer,
                DisplayName = "Player ID + Server",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.PlayerId}}}{{{AppConstants.GameFieldKeys.Server}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.PlayerId, "Player ID", "Masukkan Player ID", AppConstants.GameInputFieldTypes.Number, order: 1),
                    Field(AppConstants.GameFieldKeys.Server, "Server", "Masukkan Server", AppConstants.GameInputFieldTypes.Number, order: 2)
                ]
            },
            new()
            {
                Type = GameTemplateType.RoleId,
                DisplayName = "Role ID",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.RoleId}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.RoleId, "Role ID", "Masukkan Role ID", AppConstants.GameInputFieldTypes.Text, order: 1)
                ]
            },
            new()
            {
                Type = GameTemplateType.Username,
                DisplayName = "Username",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.Username}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.Username, "Username", "Masukkan Username", AppConstants.GameInputFieldTypes.Text, order: 1)
                ]
            },
            new()
            {
                Type = GameTemplateType.RiotId,
                DisplayName = "Riot ID",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.RiotId}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.RiotId, "Riot ID", "Masukkan Riot ID", AppConstants.GameInputFieldTypes.Text, order: 1)
                ]
            },
            new()
            {
                Type = GameTemplateType.IdPlatform,
                DisplayName = "ID Platform",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.IdPlatform}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.IdPlatform, "ID Platform", "Masukkan ID Platform", AppConstants.GameInputFieldTypes.Text, order: 1)
                ]
            },
            new()
            {
                Type = GameTemplateType.Custom,
                DisplayName = "Custom",
                TargetTemplate = $"{{{AppConstants.GameFieldKeys.UserId}}}",
                InputFields =
                [
                    Field(AppConstants.GameFieldKeys.UserId, "User ID", "Masukkan User ID", AppConstants.GameInputFieldTypes.Text, order: 1)
                ]
            }
        };

        return list.ToDictionary(template => template.Type);
    }

    private static GameInputField Field(
        string key,
        string label,
        string placeholder,
        string type,
        int order,
        bool required = true,
        List<string>? options = null) =>
        new()
        {
            Key = key,
            Label = label,
            Placeholder = placeholder,
            Type = type,
            Required = required,
            Order = order,
            Options = options ?? []
        };

    // Kembalikan salinan agar caller tidak memutasi preset yang di-share.
    private static List<GameInputField> CloneFields(
        IReadOnlyList<GameInputField> fields) =>
        fields
            .Select(field => new GameInputField
            {
                Key = field.Key,
                Label = field.Label,
                Placeholder = field.Placeholder,
                Type = field.Type,
                Required = field.Required,
                Order = field.Order,
                Options = [.. field.Options]
            })
            .ToList();
}
