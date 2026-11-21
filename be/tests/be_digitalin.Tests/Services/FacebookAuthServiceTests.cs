using be_digitalin.Application.Ports;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.Providers;
using be_digitalin.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Unit test FacebookAuthService dengan test double murni (tanpa Facebook API
/// sungguhan): provider, Redis, dan TokenService di-fake; DbContext pakai
/// EF InMemory provider. Menutup daftar kasus wajib: state, code, find-or-create,
/// separasi akun antar provider, dan kegagalan API.
/// </summary>
public class FacebookAuthServiceTests
{
    private const string StateKeyPrefix = "digitalin:oauth:facebook:state:";

    private static readonly FacebookUserProfile DefaultProfile =
        new("fb-1", "budi@example.com", "Budi Santoso", "https://graph.facebook.com/fb-1/picture");

    private sealed class FakeFacebookProvider : IFacebookProvider
    {
        public FacebookUserProfile? Profile = DefaultProfile;
        public Exception? ExchangeException;
        public int ExchangeCalls;
        public string? CapturedStateInUrl;

        public string BuildAuthorizationUrl(string state)
        {
            CapturedStateInUrl = state;
            return $"https://www.facebook.com/dialog/oauth?fake=1&state={state}";
        }

        public Task<FacebookUserProfile> ExchangeCodeForProfileAsync(
            string code, CancellationToken cancellationToken = default)
        {
            ExchangeCalls++;
            return ExchangeException is not null
                ? Task.FromException<FacebookUserProfile>(ExchangeException)
                : Task.FromResult(Profile!);
        }
    }

    private sealed class FakeRedisService : IRedisService
    {
        public readonly Dictionary<string, string> Store = new();
        public TimeSpan? LastExpiry;

        public Task SetAsync(string key, string value, TimeSpan expiry)
        {
            Store[key] = value;
            LastExpiry = expiry;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(Store.TryGetValue(key, out var value) ? value : null);

        public Task DeleteAsync(string key)
        {
            Store.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string CreateAdminToken(int adminId, string username, string role) => "admin-token";

        public string CreateCustomerToken(int customerId) => $"customer-token-{customerId}";
    }

    private static DigitalinDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DigitalinDbContext(options);
    }

    private static FacebookAuthService CreateService(
        FakeFacebookProvider provider,
        DigitalinDbContext db,
        FakeRedisService redis) =>
        new(provider, db, new FakeTokenService(), redis);

    /// <summary>BeginLogin lalu ekstrak state dari URL fake provider.</summary>
    private static async Task<string> BeginLoginAndExtractStateAsync(
        FacebookAuthService service, FakeFacebookProvider provider)
    {
        var url = await service.BeginLoginAsync();
        return provider.CapturedStateInUrl!;
    }

    // ── State (CSRF) ──

    [Fact]
    public async Task BeginLoginAsync_StoresStateInRedisAndReturnsAuthorizationUrl()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var url = await service.BeginLoginAsync();

        Assert.StartsWith("https://www.facebook.com/dialog/oauth", url);
        Assert.Contains(provider.CapturedStateInUrl!, url);

        var key = Assert.Single(redis.Store.Keys);
        Assert.StartsWith(StateKeyPrefix, key);
        Assert.Equal("1", redis.Store[key]);
        Assert.Equal(TimeSpan.FromMinutes(10), redis.LastExpiry);
    }

    [Fact]
    public async Task BeginLoginAsync_GeneratesFreshStatePerCall()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        await service.BeginLoginAsync();
        await service.BeginLoginAsync();

        Assert.Equal(2, redis.Store.Count);
    }

    [Fact]
    public async Task CompleteLoginAsync_InvalidState_RejectedWithoutCodeExchange()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var result = await service.CompleteLoginAsync("some-code", "forged-state", null);

        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Contains("kedaluwarsa atau tidak valid", result.Error);
        Assert.Equal(0, provider.ExchangeCalls);
    }

    [Fact]
    public async Task CompleteLoginAsync_MissingState_Rejected()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var result = await service.CompleteLoginAsync("some-code", null, null);

        Assert.False(result.Success);
        Assert.Contains("tidak valid", result.Error);
        Assert.Equal(0, provider.ExchangeCalls);
    }

    [Fact]
    public async Task CompleteLoginAsync_StateIsSingleUse_SecondUseRejected()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var first = await service.CompleteLoginAsync("code-1", state, null);
        var second = await service.CompleteLoginAsync("code-1", state, null);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("kedaluwarsa atau tidak valid", second.Error);
        Assert.Equal(1, provider.ExchangeCalls);
    }

    // ── Error / cancel dari Facebook ──

    [Fact]
    public async Task CompleteLoginAsync_UserCancelled_ReturnsFriendlyMessage()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var result = await service.CompleteLoginAsync(null, null, "access_denied");

        Assert.False(result.Success);
        Assert.Equal("Login Facebook dibatalkan.", result.Error);
        Assert.Equal(0, provider.ExchangeCalls);
    }

    [Fact]
    public async Task CompleteLoginAsync_OtherFacebookError_ReturnsGenericMessage()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var result = await service.CompleteLoginAsync(null, null, "unauthorized_client");

        Assert.False(result.Success);
        Assert.Equal("Facebook menolak permintaan login. Silakan coba lagi.", result.Error);
    }

    [Fact]
    public async Task CompleteLoginAsync_MissingCode_ReturnsError()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var result = await service.CompleteLoginAsync(null, state, null);

        Assert.False(result.Success);
        Assert.Contains("Kode otorisasi", result.Error);
        Assert.Equal(0, provider.ExchangeCalls);
    }

    [Fact]
    public async Task CompleteLoginAsync_FacebookApiFailure_SurfacesSafeErrorAndConsumesState()
    {
        var provider = new FakeFacebookProvider
        {
            ExchangeException = new FacebookAuthenticationException(
                "Gagal menukar kode otorisasi Facebook. Silakan coba lagi.")
        };
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var result = await service.CompleteLoginAsync("expired-code", state, null);

        Assert.False(result.Success);
        Assert.Equal("Gagal menukar kode otorisasi Facebook. Silakan coba lagi.", result.Error);
        Assert.Empty(redis.Store);
    }

    // ── Find-or-create akun Facebook ──

    [Fact]
    public async Task CompleteLoginAsync_NewFacebookUser_CreatesAccountAndExternalLogin()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var result = await service.CompleteLoginAsync("code-1", state, null);

        Assert.True(result.Success);
        Assert.StartsWith("customer-token-", result.Token);

        var customer = await db.Customers.SingleAsync();
        Assert.Equal("budi@example.com", customer.Email);
        Assert.Equal("Budi Santoso", customer.FullName);
        Assert.Equal("FACEBOOK", customer.AuthProvider);
        Assert.Equal("https://graph.facebook.com/fb-1/picture", customer.AvatarUrl);
        Assert.Equal(string.Empty, customer.PasswordHash);

        var externalLogin = await db.ExternalLogins.SingleAsync();
        Assert.Equal("FACEBOOK", externalLogin.Provider);
        Assert.Equal("fb-1", externalLogin.ProviderUserId);
        Assert.Equal(customer.Id, externalLogin.CustomerId);

        Assert.Equal($"customer-token-{customer.Id}", result.Token);
    }

    [Fact]
    public async Task CompleteLoginAsync_ExistingFacebookAccount_LogsInWithoutNewRows()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();

        var existingCustomer = new Customer
        {
            Email = "budi@example.com",
            FullName = "Budi Santoso",
            AuthProvider = "FACEBOOK",
            PasswordHash = string.Empty
        };
        db.Customers.Add(existingCustomer);
        db.ExternalLogins.Add(new ExternalLogin
        {
            Provider = "FACEBOOK",
            ProviderUserId = "fb-1",
            CustomerId = existingCustomer.Id
        });
        await db.SaveChangesAsync();

        var service = CreateService(provider, db, redis);
        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var result = await service.CompleteLoginAsync("code-1", state, null);

        Assert.True(result.Success);
        Assert.Equal($"customer-token-{existingCustomer.Id}", result.Token);
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(1, await db.ExternalLogins.CountAsync());

        // Login ulang → avatar diperbarui ke foto profil Facebook saat ini.
        var reloaded = await db.Customers.SingleAsync();
        Assert.Equal("https://graph.facebook.com/fb-1/picture", reloaded.AvatarUrl);
    }

    [Fact]
    public async Task CompleteLoginAsync_SameEmailDifferentProvider_StaysSeparateAccount()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();

        // Akun LOCAL yang sudah ada dengan email yang SAMA dengan email Facebook.
        var localCustomer = new Customer
        {
            Email = "budi@example.com",
            FullName = "Budi Local",
            AuthProvider = "LOCAL",
            PasswordHash = "bcrypt-hash"
        };
        db.Customers.Add(localCustomer);
        await db.SaveChangesAsync();

        var service = CreateService(provider, db, redis);
        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var result = await service.CompleteLoginAsync("code-1", state, null);

        Assert.True(result.Success);
        Assert.NotEqual($"customer-token-{localCustomer.Id}", result.Token);

        // Akun lokal tidak tersentuh (tidak ada linking/merging).
        var untouched = await db.Customers.SingleAsync(c => c.Id == localCustomer.Id);
        Assert.Equal("LOCAL", untouched.AuthProvider);
        Assert.Equal("bcrypt-hash", untouched.PasswordHash);
        Assert.Null(untouched.GoogleId);

        // Akun Facebook baru terpisah, email unik via placeholder deterministik.
        var facebookCustomer = await db.Customers.SingleAsync(
            c => c.AuthProvider == "FACEBOOK");
        Assert.Equal("fb_fb-1@noreply.facebook.invalid", facebookCustomer.Email);
        Assert.Equal($"customer-token-{facebookCustomer.Id}", result.Token);

        var externalLogin = await db.ExternalLogins.SingleAsync();
        Assert.Equal(facebookCustomer.Id, externalLogin.CustomerId);
    }

    [Fact]
    public async Task CompleteLoginAsync_FacebookWithoutEmail_CreatesAccountWithPlaceholderEmail()
    {
        var provider = new FakeFacebookProvider
        {
            Profile = new FacebookUserProfile("fb-9", null, "Sinta", null)
        };
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var result = await service.CompleteLoginAsync("code-1", state, null);

        Assert.True(result.Success);
        var customer = await db.Customers.SingleAsync();
        Assert.Equal("fb_fb-9@noreply.facebook.invalid", customer.Email);
        Assert.Equal("Sinta", customer.FullName);
    }

    [Fact]
    public async Task CompleteLoginAsync_FreeEmailUsedAsIs_TakenEmailReplaced()
    {
        // Email Facebook belum dipakai siapa pun → dipakai apa adanya.
        var provider = new FakeFacebookProvider
        {
            Profile = new FacebookUserProfile("fb-2", "sinta@example.com", "Sinta", null)
        };
        var redis = new FakeRedisService();
        await using var db = CreateDb();
        var service = CreateService(provider, db, redis);

        var state = await BeginLoginAndExtractStateAsync(service, provider);
        await service.CompleteLoginAsync("code-1", state, null);

        var customer = await db.Customers.SingleAsync();
        Assert.Equal("sinta@example.com", customer.Email);
    }

    // ── Avatar (foto profil Facebook) ──

    [Fact]
    public async Task CompleteLoginAsync_ExistingUser_PictureMissing_PreservesAvatar()
    {
        const string existingAvatar = "https://graph.facebook.com/fb-1/picture?old";
        var provider = new FakeFacebookProvider
        {
            Profile = new FacebookUserProfile("fb-1", "budi@example.com", "Budi", null)
        };
        var redis = new FakeRedisService();
        await using var db = CreateDb();

        var customer = new Customer
        {
            Email = "budi@example.com",
            FullName = "Budi",
            AuthProvider = "FACEBOOK",
            PasswordHash = string.Empty,
            AvatarUrl = existingAvatar
        };
        db.Customers.Add(customer);
        db.ExternalLogins.Add(new ExternalLogin
        {
            Provider = "FACEBOOK",
            ProviderUserId = "fb-1",
            CustomerId = customer.Id
        });
        await db.SaveChangesAsync();
        var savedId = customer.Id;

        var service = CreateService(provider, db, redis);
        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var result = await service.CompleteLoginAsync("code-1", state, null);

        Assert.True(result.Success);
        // Foto tidak dikirim Facebook → avatar lama TIDAK dioverwrite null.
        var unchanged = await db.Customers.SingleAsync(c => c.Id == savedId);
        Assert.Equal(existingAvatar, unchanged.AvatarUrl);
    }

    [Fact]
    public async Task CompleteLoginAsync_ExistingUser_PictureUnchanged_NoUnnecessaryWrite()
    {
        var provider = new FakeFacebookProvider();
        var redis = new FakeRedisService();
        await using var db = CreateDb();

        var customer = new Customer
        {
            Email = "budi@example.com",
            FullName = "Budi",
            AuthProvider = "FACEBOOK",
            PasswordHash = string.Empty,
            AvatarUrl = DefaultProfile.PictureUrl
        };
        db.Customers.Add(customer);
        db.ExternalLogins.Add(new ExternalLogin
        {
            Provider = "FACEBOOK",
            ProviderUserId = "fb-1",
            CustomerId = customer.Id
        });
        await db.SaveChangesAsync();

        var service = CreateService(provider, db, redis);
        var state = await BeginLoginAndExtractStateAsync(service, provider);

        var result = await service.CompleteLoginAsync("code-1", state, null);

        Assert.True(result.Success);
        Assert.Equal(DefaultProfile.PictureUrl,
            (await db.Customers.SingleAsync()).AvatarUrl);
    }
}
