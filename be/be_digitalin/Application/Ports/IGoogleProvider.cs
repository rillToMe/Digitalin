using Google.Apis.Auth;

namespace be_digitalin.Application.Ports;

/// <summary>
/// Port verifikasi ID token Google. Implemented by
/// <c>be_digitalin.Providers.GoogleProvider</c>.
/// </summary>
public interface IGoogleProvider
{
    Task<GoogleJsonWebSignature.Payload> VerifyIdTokenAsync(string idToken);
}