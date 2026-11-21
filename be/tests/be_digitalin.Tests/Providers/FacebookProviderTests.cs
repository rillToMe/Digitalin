using System.ComponentModel.DataAnnotations;
using be_digitalin.Application.Ports;
using be_digitalin.Config;
using be_digitalin.Providers;
using Microsoft.Extensions.Options;
using Xunit;

namespace be_digitalin.Tests.Providers;

/// <summary>
/// Unit test FacebookProvider tanpa panggilan HTTP sungguhan: fake
/// HttpMessageHandler menjawab token-exchange dan /me. Meliputi pembentukan
/// authorization URL dari konfigurasi, kegagalan exchange/profil, dan
/// validasi konfigurasi.
/// </summary>
public class FacebookProviderTests
{
    private const string TestAppId = "1234567890";
    private const string TestAppSecret = "test-app-secret";
    private const string TestRedirectUri = "http://localhost:5210/api/v1/auth/facebook/callback";

    private static FacebookAuthSettings ValidSettings => new()
    {
        AppId = TestAppId,
        AppSecret = TestAppSecret,
        RedirectUri = TestRedirectUri,
        FrontendRedirectUri = "http://localhost:5173/auth/facebook/callback"
    };

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public readonly List<HttpRequestMessage> Requests = new();

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    private static (FacebookProvider Provider, FakeHttpMessageHandler Handler) CreateProvider(
        FacebookAuthSettings settings, params (string urlContains, string json, int status)[] responses)
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            foreach (var (urlContains, json, status) in responses)
            {
                if (request.RequestUri!.ToString().Contains(urlContains))
                {
                    return new HttpResponseMessage((System.Net.HttpStatusCode)status)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        return (new FacebookProvider(Options.Create(settings), new HttpClient(handler)), handler);
    }

    // ── Authorization URL dari konfigurasi ──

    [Fact]
    public void BuildAuthorizationUrl_UsesConfiguredValues()
    {
        var (provider, _) = CreateProvider(ValidSettings);

        var url = provider.BuildAuthorizationUrl("state-abc");

        Assert.StartsWith("https://www.facebook.com/dialog/oauth?", url);
        Assert.Contains("client_id=1234567890", url);
        Assert.Contains(
            $"redirect_uri={Uri.EscapeDataString(TestRedirectUri)}", url);
        Assert.Contains("state=state-abc", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("scope=email", url);
        Assert.Contains("public_profile", url);
    }

    [Fact]
    public void BuildAuthorizationUrl_NotConfigured_ThrowsSafeError()
    {
        var settings = ValidSettings;
        settings.AppId = "";
        var (provider, _) = CreateProvider(settings);

        var ex = Assert.Throws<FacebookAuthenticationException>(
            () => provider.BuildAuthorizationUrl("state-abc"));

        Assert.Contains("belum dikonfigurasi", ex.Message);
    }

    // ── Token exchange + profil (HTTP di-fake) ──

    [Fact]
    public async Task ExchangeCodeForProfileAsync_Success_ReturnsProfile()
    {
        var (provider, handler) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"access_token":"AT123","token_type":"bearer"}""", 200),
            ("/me", """{"id":"42","name":"Sinta","email":"sinta@example.com","picture":{"data":{"height":200,"is_silhouette":false,"url":"https://platform-lookaside.fbsbx.com/platform/profilepic/42","width":200}}}""", 200));

        var profile = await provider.ExchangeCodeForProfileAsync("auth-code-1");

        Assert.Equal("42", profile.Id);
        Assert.Equal("sinta@example.com", profile.Email);
        Assert.Equal("Sinta", profile.Name);
        Assert.Equal("https://platform-lookaside.fbsbx.com/platform/profilepic/42", profile.PictureUrl);

        // Urutan panggilan: token exchange dulu, baru /me.
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("oauth/access_token", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("/me", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_TokenExchangeHttpError_Throws()
    {
        var (provider, handler) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"error":{"message":"code expired"}}""", 400));

        var ex = await Assert.ThrowsAsync<FacebookAuthenticationException>(
            () => provider.ExchangeCodeForProfileAsync("expired-code"));

        Assert.Contains("Gagal menukar kode otorisasi Facebook", ex.Message);
        // Gagal exchange → /me tidak pernah dipanggil.
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_TokenExchangeNoTokenInBody_Throws()
    {
        var (provider, _) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"error":{"message":"invalid"}}""", 200));

        var ex = await Assert.ThrowsAsync<FacebookAuthenticationException>(
            () => provider.ExchangeCodeForProfileAsync("weird-code"));

        Assert.Contains("Gagal menukar kode otorisasi Facebook", ex.Message);
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_ProfileRequestFails_Throws()
    {
        var (provider, _) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"access_token":"AT123"}""", 200),
            ("/me", """{"error":{"message":"Permissions error"}}""", 500));

        var ex = await Assert.ThrowsAsync<FacebookAuthenticationException>(
            () => provider.ExchangeCodeForProfileAsync("auth-code-1"));

        Assert.Contains("Gagal mengambil profil Facebook", ex.Message);
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_ProfileWithoutId_Throws()
    {
        var (provider, _) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"access_token":"AT123"}""", 200),
            ("/me", """{"name":"Sinta"}""", 200));

        var ex = await Assert.ThrowsAsync<FacebookAuthenticationException>(
            () => provider.ExchangeCodeForProfileAsync("auth-code-1"));

        Assert.Contains("Gagal mengambil profil Facebook", ex.Message);
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_ProfileWithoutEmail_EmailIsNull()
    {
        var (provider, _) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"access_token":"AT123"}""", 200),
            ("/me", """{"id":"42","name":"Sinta"}""", 200));

        var profile = await provider.ExchangeCodeForProfileAsync("auth-code-1");

        Assert.Null(profile.Email);
        Assert.Equal("42", profile.Id);
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_ProfileWithoutPicture_PictureUrlIsNull()
    {
        var (provider, _) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"access_token":"AT123"}""", 200),
            ("/me", """{"id":"42","name":"Sinta","picture":{"data":{"is_silhouette":true}}}""", 200));

        var profile = await provider.ExchangeCodeForProfileAsync("auth-code-1");

        Assert.Null(profile.PictureUrl);
        Assert.Equal("42", profile.Id);
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_RequestsPictureFieldInSingleCall()
    {
        var (provider, handler) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"access_token":"AT123"}""", 200),
            ("/me", """{"id":"42","picture":{"data":{"url":"https://x/pic"}}}""", 200));

        await provider.ExchangeCodeForProfileAsync("auth-code-1");

        // Foto diambil dari respons /me yang sama — tidak ada panggilan HTTP
        // tambahan khusus avatar.
        var meRequest = handler.Requests.Single(r =>
            r.RequestUri!.ToString().Contains("/me"));
        Assert.Contains("picture", meRequest.RequestUri!.ToString());
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_SendsSecretOnlyToTokenEndpoint()
    {
        var (provider, handler) = CreateProvider(ValidSettings,
            ("oauth/access_token", """{"access_token":"AT123"}""", 200),
            ("/me", """{"id":"42","name":"Sinta"}""", 200));

        await provider.ExchangeCodeForProfileAsync("auth-code-1");

        var urls = handler.Requests.Select(r => r.RequestUri!.ToString()).ToList();

        Assert.Equal(2, urls.Count);
        Assert.Contains("oauth/access_token", urls[0]);
        Assert.Contains(Uri.EscapeDataString(TestAppSecret), urls[0]);
        // AppSecret hanya boleh muncul di server-call token exchange.
        Assert.DoesNotContain(Uri.EscapeDataString(TestAppSecret), urls[1]);
    }

    // ── Validasi konfigurasi (Options pattern / DataAnnotations) ──

    [Fact]
    public void FacebookAuthSettings_MissingRequiredFields_FailsValidation()
    {
        var settings = new FacebookAuthSettings();
        var context = new ValidationContext(settings);
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(settings, context, results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void FacebookAuthSettings_Complete_PassesValidation()
    {
        var settings = ValidSettings;
        var context = new ValidationContext(settings);
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(settings, context, results, validateAllProperties: true);

        Assert.True(valid);
        Assert.Empty(results);
    }
}
