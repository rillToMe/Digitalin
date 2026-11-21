namespace be_digitalin.Application.Ports;

/// <summary>
/// Profil Google hasil verifikasi server-side (ID Token).
/// Bebas dari tipe SDK Google.Apis.Auth agar layer Application tidak bergantung
/// pada library infrastruktur eksternal.
/// </summary>
public sealed record GoogleUserProfile(
    string Subject,
    string Email,
    string? Name,
    string? Picture);

/// <summary>
/// Port verifikasi ID token Google. Implemented by
/// <c>be_digitalin.Providers.GoogleProvider</c>.
/// </summary>
public interface IGoogleProvider
{
    Task<GoogleUserProfile> VerifyIdTokenAsync(string idToken);
}
