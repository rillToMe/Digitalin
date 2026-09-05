using be_digitalin.Models;

namespace be_digitalin.DTOs;

/// <summary>Respons GET /admin/system/midtrans-expiry — pengganti `new { expiry }`.</summary>
public record MidtransExpiryResponse(int Expiry);

/// <summary>Respons GET /admin/system/popular-games — pengganti `new { selected, options }`.</summary>
public record PopularGamesResponse(
    IEnumerable<CategoryInfo> Selected,
    IEnumerable<GameMetadataResponse> Options);

/// <summary>Respons PUT /admin/system/popular-games — pengganti `new { message, selected }`.</summary>
public record PopularGamesSavedResponse(
    string Message,
    IEnumerable<CategoryInfo> Selected);

/// <summary>Respons PUT /admin/system/game-categories — pengganti `new { message, categories }`.</summary>
public record GameCategoriesSavedResponse(
    string Message,
    IEnumerable<GameCategoryInfo> Categories);

/// <summary>Respons PUT /admin/system/metadata/{id} — pengganti `new { message, metadata }`.</summary>
public record GameMetadataSavedResponse(
    string Message,
    GameMetadataResponse Metadata);

/// <summary>Satu baris preset template game — pengganti object anonymous di GET /admin/system/game-templates.</summary>
public record GameTemplateResponse(
    string Type,
    string DisplayName,
    string TargetTemplate,
    IReadOnlyList<GameInputField> InputFields);