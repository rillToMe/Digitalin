namespace be_digitalin.DTOs;

/// <summary>Respons token JWT — pengganti `new { token }`.</summary>
public record TokenResponse(string Token);

/// <summary>
/// Profil customer. Field `CreatetAt` sengaja TYPO (bukan `CreatedAt`) —
/// kontrak API_CONTRACT.md mengharuskan JSON `createtAt`.
/// </summary>
public record ProfileResponse(
    string Email,
    string FullName,
    DateTime CreatetAt);