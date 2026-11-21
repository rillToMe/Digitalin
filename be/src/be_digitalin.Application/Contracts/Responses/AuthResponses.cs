namespace be_digitalin.DTOs;

using System.Text.Json.Serialization;

/// <summary>Respons token JWT — pengganti `new { token }`.</summary>
public record TokenResponse(string Token);

/// <summary>
/// Profil customer. Properti C# bersih (`CreatedAt`), tapi nama JSON dipertahankan
/// `createtAt` - kontrak API_CONTRACT.md (dan FE) sudah terlanjur memakai nama itu.
/// </summary>
public record ProfileResponse(
    string Email,
    string FullName,
    [property: JsonPropertyName("createtAt")] DateTime CreatedAt,
    /// <summary>URL foto profil (OAuth Google/Facebook); null untuk user tanpa avatar.</summary>
    string? AvatarUrl = null);