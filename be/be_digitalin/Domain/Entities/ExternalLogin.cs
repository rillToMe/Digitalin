namespace be_digitalin.Domain.Entities;

/// <summary>
/// Identitas akun dari provider OAuth eksternal (saat ini Facebook, dirancang
/// generik untuk provider berikutnya). Identity = pasangan (Provider,
/// ProviderUserId) — BUKAN email. Konsekuensi kebijakan saat ini: satu identitas
/// provider = satu akun Digitalin terpisah, tidak ada linking/merging lintas
/// provider meski email sama (account linking direncanakan terpisah nanti).
/// </summary>
public class ExternalLogin
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    /// <summary>Nama provider, mis. "FACEBOOK". Analog AuthProvider di Customer.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>User ID unik dari provider (Facebook user id — stabil, bukan email).</summary>
    public string ProviderUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
