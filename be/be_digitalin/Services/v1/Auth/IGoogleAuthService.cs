namespace be_digitalin.Services.Auth;

public interface IGoogleAuthService
{
    /// <summary>
    /// Verifies Google ID token, registers or logs in the customer,
    /// and returns a JWT using the same mechanism as local login.
    /// </summary>
    /// <param name="idToken">The Google ID token from the frontend.</param>
    /// <returns>
    /// An <see cref="AuthResponse"/> containing the JWT on success,
    /// or null if verification fails.
    /// </returns>
    Task<AuthResponse?> AuthenticateWithGoogleAsync(string idToken);
}

public record AuthResponse(string Token);
