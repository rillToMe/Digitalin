using System.Security.Cryptography;
using be_digitalin.Application.Ports;
using be_digitalin.Domain.Entities;
using be_digitalin.Providers;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Auth;

/// <summary>
/// Orkestrasi Facebook OAuth (authorization code flow) — kebalikan
/// GoogleAuthService yang memverifikasi ID token. Endpoint HTTP, AppSecret,
/// dan format URL Facebook disembunyikan di <see cref="IFacebookProvider"/>;
/// layanan ini hanya mengurus state CSRF, find-or-create akun, dan JWT.
///
/// Kebijakan akun (beda dengan Google — sesuai requirement Facebook saat ini):
/// identity = pasangan (Provider, ProviderUserId) di tabel ExternalLogin.
/// TIDAK ada linking/merging berdasarkan email — email yang sudah dipakai
/// akun lokal/Google tetap menghasilkan akun Facebook baru yang terpisah.
/// </summary>
public class FacebookAuthService : IFacebookAuthService
{
    public const string ProviderName = "FACEBOOK";

    // State OAuth: cukup untuk roundtrip ke Facebook, sekali pakai.
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private readonly IFacebookProvider _facebookProvider;
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IRedisService _redis;

    public FacebookAuthService(
        IFacebookProvider facebookProvider,
        IApplicationDbContext db,
        ITokenService tokenService,
        IRedisService redis)
    {
        _facebookProvider = facebookProvider;
        _db = db;
        _tokenService = tokenService;
        _redis = redis;
    }

    public async Task<string> BeginLoginAsync()
    {
        var state = GenerateState();

        // Value "1" — keberadaan key yang penting (issued by server ini),
        // state-nya sendiri sudah acak dan tidak perlu dibaca balik.
        await _redis.SetAsync(StateKey(state), "1", StateLifetime);

        return _facebookProvider.BuildAuthorizationUrl(state);
    }

    public async Task<FacebookAuthResult> CompleteLoginAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default)
    {
        // 1. User membatalkan / menolak izin di halaman Facebook
        //    (Facebook kirim error=access_denied tanpa code).
        if (!string.IsNullOrWhiteSpace(error))
        {
            return new FacebookAuthResult(
                null,
                error == "access_denied"
                    ? "Login Facebook dibatalkan."
                    : "Facebook menolak permintaan login. Silakan coba lagi.");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            return new FacebookAuthResult(
                null, "Sesi login Facebook tidak valid. Silakan coba lagi.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return new FacebookAuthResult(
                null, "Kode otorisasi Facebook tidak ditemukan. Silakan coba lagi.");
        }

        // 2. Validasi state CSRF: harus diterbitkan server ini dan belum
        //    kedaluwarsa. Hapus SEBELUM exchange supaya state/code tidak bisa
        //    dipakai ulang (sekali pakai, walau langkah berikutnya gagal).
        var stored = await _redis.GetAsync(StateKey(state));
        if (stored is null)
        {
            return new FacebookAuthResult(
                null, "Sesi login Facebook kedaluwarsa atau tidak valid. Silakan coba lagi.");
        }

        await _redis.DeleteAsync(StateKey(state));

        // 3. Tukar code → access token → profil (server-to-server).
        FacebookUserProfile profile;
        try
        {
            profile = await _facebookProvider.ExchangeCodeForProfileAsync(
                code, cancellationToken);
        }
        catch (FacebookAuthenticationException ex)
        {
            return new FacebookAuthResult(null, ex.Message);
        }

        // 4. Login / auto-register berdasarkan identitas provider — bukan email.
        var customer = await FindOrCreateCustomerAsync(profile, cancellationToken);

        // 5. JWT dengan mekanisme yang sama dengan login lokal/Google.
        return new FacebookAuthResult(
            _tokenService.CreateCustomerToken(customer.Id), null);
    }

    private async Task<Domain.Entities.Customer> FindOrCreateCustomerAsync(
        FacebookUserProfile profile,
        CancellationToken cancellationToken)
    {
        var existing = await FindByProviderIdentityAsync(profile.Id, cancellationToken);
        if (existing is not null)
        {
            // Akun Facebook sudah ada — login biasa. Avatar diperbarui ke foto
            // profil Facebook saat ini bila ada (bila tidak ada, avatar lama
            // dipertahankan — tidak dioverwrite null).
            await ApplyAvatarAsync(existing, profile.PictureUrl, cancellationToken);
            return existing;
        }

        var email = await ResolveEmailAsync(profile, cancellationToken);

        var customer = new Domain.Entities.Customer
        {
            Email = email,
            FullName = ResolveFullName(profile, email),
            AuthProvider = ProviderName,
            AvatarUrl = profile.PictureUrl,
            // Akun Facebook tidak punya password lokal — sama dengan akun Google.
            PasswordHash = string.Empty
        };

        var externalLogin = new ExternalLogin
        {
            Provider = ProviderName,
            ProviderUserId = profile.Id,
            Customer = customer
        };

        _db.Customers.Add(customer);
        _db.ExternalLogins.Add(externalLogin);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Kalah race melawan login Facebook paralel dengan user id sama:
            // unique index (Provider, ProviderUserId) menolak insert kedua.
            // Detach instance gagal, ambil baris yang dibuat request lain,
            // lanjut login normal — tanpa 500 dan tanpa duplikat.
            // (Pola yang sama dengan GoogleAuthService.)
            _db.Entry(customer).State = EntityState.Detached;
            _db.Entry(externalLogin).State = EntityState.Detached;

            existing = await FindByProviderIdentityAsync(profile.Id, cancellationToken);
            if (existing is null)
                throw;
        }

        return existing ?? customer;
    }

    /// <summary>
    /// Update avatar ke foto profil Facebook saat ini. Foto tidak ada / kosong →
    /// avatar lama dipertahankan (tidak dioverwrite dengan null).
    /// Write DB hanya bila nilai benar-benar berubah.
    /// </summary>
    private async Task ApplyAvatarAsync(
        Domain.Entities.Customer customer,
        string? pictureUrl,
        CancellationToken cancellationToken)
    {
        if (pictureUrl is null || customer.AvatarUrl == pictureUrl)
            return;

        customer.AvatarUrl = pictureUrl;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // Tracked (tanpa AsNoTracking) supaya update avatar terdeteksi SaveChanges.
    private Task<Domain.Entities.Customer?> FindByProviderIdentityAsync(
        string providerUserId,
        CancellationToken cancellationToken) =>
        _db.ExternalLogins
            .Include(e => e.Customer)
            .Where(e => e.Provider == ProviderName && e.ProviderUserId == providerUserId)
            .Select(e => e.Customer)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Email Facebook dipakai apa adanya bila belum dipakai akun lain. Email
    /// yang sudah terpakai (akun lokal/Google) TIDAK dipinjam — akun Facebook
    /// tetap dibuat terpisah (kebijakan no-linking), sehingga butu placeholder
    /// deterministik per Facebook user id untuk lolos unique index Email.
    /// Facebook juga bisa tidak mengirim email sama sekali (user menolak izin).
    /// </summary>
    private async Task<string> ResolveEmailAsync(
        FacebookUserProfile profile,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(profile.Email) &&
            !await _db.Customers.AnyAsync(
                c => c.Email == profile.Email, cancellationToken))
        {
            return profile.Email;
        }

        return $"fb_{profile.Id}@noreply.facebook.invalid";
    }

    // Sama dengan fallback Google: nama profil, kalau kosong pakai email.
    private static string ResolveFullName(FacebookUserProfile profile, string email) =>
        string.IsNullOrWhiteSpace(profile.Name) ? email : profile.Name;

    private static string StateKey(string state) =>
        $"digitalin:oauth:facebook:state:{state}";

    // 32 byte CSPRNG → base64url (~43 karakter, URL-safe tanpa padding).
    private static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
