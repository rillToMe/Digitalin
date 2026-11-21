namespace be_digitalin.Services.Auth;

public interface IFacebookAuthService
{
    /// <summary>
    /// Mulai OAuth flow: terbitkan state CSRF (disimpan di Redis, TTL 10
    /// menit) dan kembalikan URL otorisasi Facebook untuk di-redirect-kan
    /// browser (GET /api/v1/auth/facebook/login).
    /// </summary>
    Task<string> BeginLoginAsync();

    /// <summary>
    /// Selesaikan OAuth flow di callback: validasi state (CSRF + sekali
    /// pakai), tukar code dengan profil Facebook, login / auto-register akun
    /// Facebook (identity = provider user id, bukan email), lalu terbitkan
    /// JWT dengan mekanisme yang sama dengan login lokal/Google.
    /// Gagal → <see cref="FacebookAuthResult.Error"/> berisi pesan aman
    /// untuk user (tidak ada detail internal).
    /// </summary>
    Task<FacebookAuthResult> CompleteLoginAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default);
}

/// <summary>Token JWT saat sukses; pesan error user-friendly saat gagal.</summary>
public sealed record FacebookAuthResult(string? Token, string? Error)
{
    public bool Success => Token is not null;
}
