using be_digitalin.Application.Ports;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.Providers;
using be_digitalin.Services.Auth;
using be_digitalin.Services.Customer;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Security;
using be_digitalin.Config;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Unit test GoogleAuthService (avatar) + CustomerService (registrasi lokal tanpa
/// avatar) dengan test double murni — tanpa panggilan Google API sungguhan.
/// Payload Google dibuat manual: service hanya membaca properti terverifikasi
/// dari objek yang dikembalikan provider (avatar TIDAK pernah dari klien).
/// </summary>
public class GoogleAuthServiceTests
{
    private sealed class FakeGoogleProvider : IGoogleProvider
    {
        public GoogleJsonWebSignature.Payload Payload { get; set; } = NewPayload();

        public Task<GoogleJsonWebSignature.Payload> VerifyIdTokenAsync(string idToken) =>
            Task.FromResult(Payload);
    }

    private static GoogleJsonWebSignature.Payload NewPayload(
        string subject = "g-1",
        string email = "budi@gmail.com",
        string? name = "Budi Gmail",
        string? picture = "https://lh3.googleusercontent.com/photo-g1") =>
        new()
        {
            Subject = subject,
            Email = email,
            Name = name,
            Picture = picture
        };

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

    // ── Avatar dari profil Google terverifikasi ──

    [Fact]
    public async Task AuthenticateWithGoogleAsync_NewUser_AvatarUrlFromGooglePicture()
    {
        var provider = new FakeGoogleProvider();
        await using var db = CreateDb();
        var service = new GoogleAuthService(provider, db, new FakeTokenService());

        var result = await service.AuthenticateWithGoogleAsync("id-token");

        Assert.NotNull(result);
        var customer = await db.Customers.SingleAsync();
        Assert.Equal("budi@gmail.com", customer.Email);
        Assert.Equal("GOOGLE", customer.AuthProvider);
        Assert.Equal("https://lh3.googleusercontent.com/photo-g1", customer.AvatarUrl);
        Assert.Equal($"customer-token-{customer.Id}", result!.Token);
    }

    [Fact]
    public async Task AuthenticateWithGoogleAsync_NewUser_NoPicture_AvatarUrlNull()
    {
        var provider = new FakeGoogleProvider { Payload = NewPayload(picture: null) };
        await using var db = CreateDb();
        var service = new GoogleAuthService(provider, db, new FakeTokenService());

        await service.AuthenticateWithGoogleAsync("id-token");

        var customer = await db.Customers.SingleAsync();
        Assert.Null(customer.AvatarUrl);
    }

    [Fact]
    public async Task AuthenticateWithGoogleAsync_ExistingGoogleUser_AvatarUpdatedOnLogin()
    {
        var provider = new FakeGoogleProvider();
        await using var db = CreateDb();

        // User Google lama dengan avatar usang.
        var customer = new Customer
        {
            Email = "budi@gmail.com",
            FullName = "Budi",
            GoogleId = "g-1",
            AuthProvider = "GOOGLE",
            PasswordHash = string.Empty,
            AvatarUrl = "https://lh3.googleusercontent.com/photo-old"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var id = customer.Id;

        var service = new GoogleAuthService(provider, db, new FakeTokenService());

        var result = await service.AuthenticateWithGoogleAsync("id-token");

        Assert.Equal($"customer-token-{id}", result!.Token);
        var reloaded = await db.Customers.SingleAsync(c => c.Id == id);
        Assert.Equal("https://lh3.googleusercontent.com/photo-g1", reloaded.AvatarUrl);
    }

    [Fact]
    public async Task AuthenticateWithGoogleAsync_ExistingLocalAccount_LinkCaseAlsoSetsAvatar()
    {
        var provider = new FakeGoogleProvider();
        await using var db = CreateDb();

        var customer = new Customer
        {
            Email = "budi@gmail.com",
            FullName = "Budi Local",
            AuthProvider = "LOCAL",
            PasswordHash = "bcrypt-hash"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var id = customer.Id;

        var service = new GoogleAuthService(provider, db, new FakeTokenService());

        var result = await service.AuthenticateWithGoogleAsync("id-token");

        Assert.Equal($"customer-token-{id}", result!.Token);
        var linked = await db.Customers.SingleAsync(c => c.Id == id);
        Assert.Equal("g-1", linked.GoogleId);
        Assert.Equal("https://lh3.googleusercontent.com/photo-g1", linked.AvatarUrl);
        // Linking + avatar → masih satu baris customer.
        Assert.Equal(1, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task AuthenticateWithGoogleAsync_PictureMissing_PreservesExistingAvatar()
    {
        var provider = new FakeGoogleProvider { Payload = NewPayload(picture: null) };
        await using var db = CreateDb();

        const string existingAvatar = "https://lh3.googleusercontent.com/photo-keep";
        var customer = new Customer
        {
            Email = "budi@gmail.com",
            FullName = "Budi",
            GoogleId = "g-1",
            AuthProvider = "GOOGLE",
            PasswordHash = string.Empty,
            AvatarUrl = existingAvatar
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var service = new GoogleAuthService(provider, db, new FakeTokenService());

        await service.AuthenticateWithGoogleAsync("id-token");

        // Google tidak mengirim picture → avatar lama TIDAK dioverwrite null.
        var unchanged = await db.Customers.SingleAsync();
        Assert.Equal(existingAvatar, unchanged.AvatarUrl);
    }

    [Fact]
    public async Task AuthenticateWithGoogleAsync_AvatarUnchanged_NoUnnecessaryWrite()
    {
        var provider = new FakeGoogleProvider();
        await using var db = CreateDb();

        var customer = new Customer
        {
            Email = "budi@gmail.com",
            FullName = "Budi",
            GoogleId = "g-1",
            AuthProvider = "GOOGLE",
            PasswordHash = string.Empty,
            AvatarUrl = "https://lh3.googleusercontent.com/photo-g1"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var service = new GoogleAuthService(provider, db, new FakeTokenService());

        var result = await service.AuthenticateWithGoogleAsync("id-token");

        Assert.NotNull(result);
        Assert.Equal("https://lh3.googleusercontent.com/photo-g1",
            (await db.Customers.SingleAsync()).AvatarUrl);
    }

    // ── Registrasi email/password: avatar tetap null ──

    private sealed class StubOrderService : IOrderService
    {
        public Task<OrderResponse> PlaceOrderAsync(CreateOrderRequest request, int? customerId = null) => throw new NotImplementedException();
        public Task<PagedResult<OrderResponse>> GetAllOrdersAsync(int page = 1, int limit = 20, string? status = null) => throw new NotImplementedException();
        public Task<PagedResult<OrderResponse>> GetOrdersAsync(int? customerId = null, int page = 1, int limit = 20, string? status = null) => throw new NotImplementedException();
        public Task<bool> HandleNotificationAsync(MidtransNotificationRequest notification) => throw new NotImplementedException();
        public Task<OrderActionResult> RetryOrderAsync(string orderNo) => throw new NotImplementedException();
        public Task<OrderActionResult> RefundOrderAsync(string orderNo) => throw new NotImplementedException();
        public Task<string> HandleDigiflazzWebhookAsync(DigiflazzWebhookData data) => throw new NotImplementedException();
        public Task SweepStuckOrdersAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StubRecaptchaService : IRecaptchaService
    {
        public Task<bool> VerifyAsync(string token, string expectedAction, double minimumScore, string? remoteIp = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubEmailProvider : IEmailProvider
    {
        public Task SendOtpEmailAsync(string targetEmail, string otpCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task SendPasswordResetOtpEmailAsync(string targetEmail, string otpCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeRedisService : IRedisService
    {
        private readonly Dictionary<string, string> _store = new();

        public Task SetAsync(string key, string value, TimeSpan expiry)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

        public Task DeleteAsync(string key)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RegisterAsync_EmailPasswordUser_AvatarUrlStaysNull()
    {
        await using var db = CreateDb();
        var redis = new FakeRedisService();
        await redis.SetAsync("digitalin:otp:registration:lokal@example.com", "123456", TimeSpan.FromMinutes(5));

        var customerService = new CustomerService(
            db,
            new FakeTokenService(),
            new StubOrderService(),
            new StubRecaptchaService(),
            Options.Create(new RecaptchaSettings()),
            new StubEmailProvider(),
            redis);

        var token = await customerService.RegisterAsync(
            "lokal@example.com", "rahasia123", "Budi Lokal", "123456");

        Assert.NotNull(token);
        var customer = await db.Customers.SingleAsync();
        Assert.Equal("lokal@example.com", customer.Email);
        Assert.Null(customer.AvatarUrl);
    }

    // ── Avatar tidak pernah diterima dari klien (kontrak request terkunci) ──

    [Theory]
    [InlineData(typeof(UpdateProfileRequest))]
    [InlineData(typeof(GoogleLoginRequest))]
    public void RequestDtos_NeverAcceptAvatarFromClient(Type dtoType)
    {
        var avatarProps = dtoType.GetProperties()
            .Where(p => p.Name.Contains("Avatar", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(avatarProps);
    }
}
