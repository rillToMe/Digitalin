using be_digitalin.Application.Ports;
using be_digitalin.Config;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace be_digitalin.Providers;

public class GoogleProvider : IGoogleProvider
{
    private readonly GoogleAuthSettings _settings;

    public GoogleProvider(IOptions<GoogleAuthSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Verifies a Google ID Token using the official Google SDK.
    /// Validates: signature, audience (ClientId), issuer, and expiration.
    /// Returns the verified payload on success.
    /// Throws GoogleAuthenticationException on any validation failure.
    /// </summary>
    public async Task<GoogleJsonWebSignature.Payload> VerifyIdTokenAsync(string idToken)
    {
        try
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _settings.ClientId }
            };

            return await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);
        }
        catch (InvalidJwtException ex)
        {
            throw new GoogleAuthenticationException("Invalid Google ID token.", ex);
        }
        catch (Exception ex) when (ex is not GoogleAuthenticationException)
        {
            throw new GoogleAuthenticationException("Google token verification failed.", ex);
        }
    }
}

/// <summary>
/// Thrown when Google ID token verification fails for any reason
/// (invalid signature, wrong audience, expired, malformed, etc.)
/// </summary>
public class GoogleAuthenticationException : Exception
{
    public GoogleAuthenticationException(string message) : base(message) { }
    public GoogleAuthenticationException(string message, Exception inner) : base(message, inner) { }
}
