namespace be_digitalin.Application.Ports;

/// <summary>Profil Facebook hasil verifikasi server-side (Graph API /me).
/// Email bisa null bila user tidak memberikan izin email; PictureUrl bisa
/// null bila Facebook tidak mengirim foto (disimpan apa adanya oleh service).</summary>
public sealed record FacebookUserProfile(
    string Id,
    string? Email,
    string? Name,
    string? PictureUrl);

/// <summary>
/// Port OAuth Facebook (authorization code flow). Implemented by
/// <c>be_digitalin.Providers.FacebookProvider</c>. Service hanya melihat
/// kontrak ini — endpoint Facebook, AppSecret, dan parsing JSON tidak bocor
/// ke layer Application.
/// </summary>
public interface IFacebookProvider
{
    /// <summary>URL dialog otorisasi Facebook (scope: email, public_profile).</summary>
    string BuildAuthorizationUrl(string state);

    /// <summary>
    /// Tukar authorization code dengan access token (server-to-server),
    /// lalu ambil profil /me. Validasi kode gagal / expired / API error
    /// dilempar sebagai FacebookAuthenticationException dengan pesan aman.
    /// </summary>
    Task<FacebookUserProfile> ExchangeCodeForProfileAsync(
        string code,
        CancellationToken cancellationToken = default);
}
