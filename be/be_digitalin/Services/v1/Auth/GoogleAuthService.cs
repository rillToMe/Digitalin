using be_digitalin.Application.Ports;
using be_digitalin.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Auth;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly IGoogleProvider _googleProvider;
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokenService;

    public GoogleAuthService(
        IGoogleProvider googleProvider,
        IApplicationDbContext db,
        ITokenService tokenService)
    {
        _googleProvider = googleProvider;
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse?> AuthenticateWithGoogleAsync(string idToken)
    {
        // 1. Verify Google ID Token using the official Google SDK
        var payload = await _googleProvider.VerifyIdTokenAsync(idToken);

        var email = payload.Email;
        var googleId = payload.Subject; // Google's unique user ID (sub claim)
        var displayName = payload.Name;
        var picture = NormalizeAvatarUrl(payload.Picture); // from verified Google profile only

        // 2. Find existing Customer by email
        var customer = await FindCustomerByEmailAsync(email);

        if (customer == null)
        {
            // 3a. Customer doesn't exist — auto-register with Google info
            customer = new Domain.Entities.Customer
            {
                Email = email,
                FullName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                GoogleId = googleId,
                AuthProvider = "GOOGLE",
                AvatarUrl = picture,
                // PasswordHash stays empty — Google accounts don't use local passwords
                PasswordHash = string.Empty
            };

            _db.Customers.Add(customer);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Kalah race melawan login Google paralel dengan email sama:
                // unique index IX_Customers_Email menolak insert kedua. Detach
                // instance gagal, ambil customer yang dibuat instance lain, dan
                // lanjut login normal — tanpa 500 dan tanpa duplikat.
                _db.Entry(customer).State = EntityState.Detached;

                customer = await FindCustomerByEmailAsync(email);

                if (customer == null)
                    throw;

                // Ambil alih avatar dari instance yang menang race.
                await ApplyAvatarAsync(customer, picture);
            }
        }
        else if (string.IsNullOrEmpty(customer.GoogleId))
        {
            // 3b. Existing local account with same email — link Google to it
            //     (no duplicate account created)
            customer.GoogleId = googleId;

            // If AuthProvider was not set (legacy data), mark it now
            if (string.IsNullOrEmpty(customer.AuthProvider))
                customer.AuthProvider = "LOCAL";

            // Avatar dari profil Google terverifikasi — satu save bersama linking.
            customer.AvatarUrl = picture ?? customer.AvatarUrl;

            await _db.SaveChangesAsync();
        }
        else
        {
            // 3c. Customer already has GoogleId — just proceed to login.
            //     Avatar diperbarui ke foto profil Google saat ini (bila ada).
            await ApplyAvatarAsync(customer, picture);
        }

        // 4. Generate JWT using the same mechanism as local login
        var token = _tokenService.CreateCustomerToken(customer.Id);
        return new AuthResponse(token);
    }

    /// <summary>
    /// Update avatar ke foto profil Google saat ini. Foto tidak ada / kosong →
    /// avatar lama dipertahankan (tidak dioverwrite dengan null).
    /// Write DB hanya bila nilai benar-benar berubah.
    /// </summary>
    private async Task ApplyAvatarAsync(Domain.Entities.Customer customer, string? picture)
    {
        if (picture is null || customer.AvatarUrl == picture)
            return;

        customer.AvatarUrl = picture;
        await _db.SaveChangesAsync();
    }

    private static string? NormalizeAvatarUrl(string? picture) =>
        string.IsNullOrWhiteSpace(picture) ? null : picture;

    private Task<Domain.Entities.Customer?> FindCustomerByEmailAsync(string email) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Email == email);
}
