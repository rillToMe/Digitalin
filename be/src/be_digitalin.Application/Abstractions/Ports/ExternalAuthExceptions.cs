namespace be_digitalin.Providers;

/// <summary>
/// Thrown when Google ID token verification fails for any reason
/// (invalid signature, wrong audience, expired, malformed, etc.)
/// </summary>
public class GoogleAuthenticationException : Exception
{
    public GoogleAuthenticationException(string message) : base(message) { }
    public GoogleAuthenticationException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when the Facebook OAuth flow fails (misconfiguration, invalid/expired
/// code, Facebook API error). Pesan aman untuk end user — detail internal
/// (token, secret, status HTTP) tidak pernah ikut keluar.
/// </summary>
public class FacebookAuthenticationException : Exception
{
    public FacebookAuthenticationException(string message) : base(message) { }
    public FacebookAuthenticationException(string message, Exception inner) : base(message, inner) { }
}
