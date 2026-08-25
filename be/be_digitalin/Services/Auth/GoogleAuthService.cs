using be_digitalin.Data;
using be_digitalin.Models;
using be_digitalin.Providers;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Auth;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleProvider _googleProvider;
    private readonly DigitalinDbContext _db;
    private readonly ITokenService _tokenService;

    public GoogleAuthService(
        GoogleProvider googleProvider,
        DigitalinDbContext db,
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

        // 2. Find existing Customer by email
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);

        if (customer == null)
        {
            // 3a. Customer doesn't exist — auto-register with Google info
            customer = new Models.Customer
            {
                Email = email,
                FullName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                GoogleId = googleId,
                AuthProvider = "GOOGLE",
                // PasswordHash stays empty — Google accounts don't use local passwords
                PasswordHash = string.Empty
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }
        else if (string.IsNullOrEmpty(customer.GoogleId))
        {
            // 3b. Existing local account with same email — link Google to it
            //     (no duplicate account created)
            customer.GoogleId = googleId;

            // If AuthProvider was not set (legacy data), mark it now
            if (string.IsNullOrEmpty(customer.AuthProvider))
                customer.AuthProvider = "LOCAL";

            await _db.SaveChangesAsync();
        }
        // 3c. Customer already has GoogleId — just proceed to login (no DB write needed)

        // 4. Generate JWT using the same mechanism as local login
        var token = _tokenService.CreateCustomerToken(customer.Id);
        return new AuthResponse(token);
    }
}
