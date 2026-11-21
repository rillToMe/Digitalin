using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Config;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers;

/// <summary>
/// Adapter OAuth Facebook berbasis HTTP (tanpa SDK Meta). Menjaga seluruh
/// pengetahuan endpoint Facebook + AppSecret di satu tempat:
///   1. GET  /dialog/oauth            → URL otorisasi (scope email, public_profile)
///   2. GET  /oauth/access_token      → tukar code jadi access token (server-side)
///   3. GET  /me?fields=id,name,email → profil user
/// AppSecret hanya dikirim ke endpoint token Facebook (HTTPS, sesuai protokol
/// OAuth Facebook) dan tidak pernah di-log / dikirim ke frontend.
/// </summary>
public class FacebookProvider : IFacebookProvider
{
    private const string AuthorizationEndpoint = "https://www.facebook.com/dialog/oauth";

    // Graph API version — kontrak API, bukan konfigurasi environment. Naikkan
    // versi saat Meta deprecate (cek https://developers.facebook.com/docs/graph-api/changelog).
    private const string GraphApiBase = "https://graph.facebook.com/v26.0";

    private const string Scopes = "email,public_profile";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly FacebookAuthSettings _settings;
    private readonly HttpClient _httpClient;

    public FacebookProvider(IOptions<FacebookAuthSettings> settings, HttpClient httpClient)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
    }

    public string BuildAuthorizationUrl(string state)
    {
        EnsureConfigured();

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _settings.AppId,
            ["redirect_uri"] = _settings.RedirectUri,
            ["state"] = state,
            ["response_type"] = "code",
            ["scope"] = Scopes
        };

        return QueryHelpers.AddQueryString(AuthorizationEndpoint, query);
    }

    public async Task<FacebookUserProfile> ExchangeCodeForProfileAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var accessToken = await ExchangeCodeForAccessTokenAsync(code, cancellationToken);
        return await FetchProfileAsync(accessToken, cancellationToken);
    }

    private async Task<string> ExchangeCodeForAccessTokenAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _settings.AppId,
            ["redirect_uri"] = _settings.RedirectUri,
            ["client_secret"] = _settings.AppSecret,
            ["code"] = code
        };

        using var response = await _httpClient.GetAsync(
            QueryHelpers.AddQueryString($"{GraphApiBase}/oauth/access_token", query),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Kode invalid/expired atau kredensial salah — detail internal
            // (status, body) tidak dibocorkan ke caller.
            throw new FacebookAuthenticationException(
                "Gagal menukar kode otorisasi Facebook. Silakan coba lagi.");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(
            JsonOptions, cancellationToken);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new FacebookAuthenticationException(
                "Gagal menukar kode otorisasi Facebook. Silakan coba lagi.");
        }

        return payload.AccessToken;
    }

    private async Task<FacebookUserProfile> FetchProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            // picture.type(large) ≈ 200x200 — URL foto ikut dalam respons /me
            // (tidak perlu panggilan HTTP tambahan). Nesting Graph:
            //   "picture": { "data": { "url": "..." } }
            ["fields"] = "id,name,email,picture.type(large)",
            ["access_token"] = accessToken
        };

        using var response = await _httpClient.GetAsync(
            QueryHelpers.AddQueryString($"{GraphApiBase}/me", query),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FacebookAuthenticationException(
                "Gagal mengambil profil Facebook. Silakan coba lagi.");
        }

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>(
            JsonOptions, cancellationToken);

        if (profile is null || string.IsNullOrWhiteSpace(profile.Id))
        {
            throw new FacebookAuthenticationException(
                "Gagal mengambil profil Facebook. Silakan coba lagi.");
        }

        return new FacebookUserProfile(
            profile.Id,
            string.IsNullOrWhiteSpace(profile.Email) ? null : profile.Email,
            string.IsNullOrWhiteSpace(profile.Name) ? null : profile.Name,
            string.IsNullOrWhiteSpace(profile.Picture?.Data?.Url) ? null : profile.Picture.Data.Url);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.AppId) ||
            string.IsNullOrWhiteSpace(_settings.AppSecret) ||
            string.IsNullOrWhiteSpace(_settings.RedirectUri))
        {
            throw new FacebookAuthenticationException(
                "Login Facebook belum dikonfigurasi di server.");
        }
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")]
        string? AccessToken);

    // Nesting respons /me: picture → data → url (lihat comment FetchProfileAsync).
    private sealed record ProfileResponse(
        string? Id,
        string? Email,
        string? Name,
        PictureNode? Picture);

    private sealed record PictureNode(PictureData? Data);

    private sealed record PictureData(string? Url);
}
