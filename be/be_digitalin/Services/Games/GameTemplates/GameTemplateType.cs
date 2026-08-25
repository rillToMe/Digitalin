namespace be_digitalin.Services.Games.GameTemplates;

/// <summary>
/// Preset input template untuk sebuah game.
/// Setiap tipe memetakan ke satu set InputFields + TargetTemplate
/// yang di-resolve oleh <see cref="GameTemplateFactory"/>.
/// </summary>
public enum GameTemplateType
{
    UserId,
    UserIdServer,
    UserIdServerPipe,
    UserIdZone,
    PlayerId,
    PlayerIdServer,
    RoleId,
    Username,
    RiotId,
    IdPlatform,
    Custom
}
