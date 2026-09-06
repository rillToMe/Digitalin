using System.Data;

namespace be_digitalin.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Google OAuth fields
    public string? GoogleId { get; set; }
    public string AuthProvider { get; set; } = "LOCAL";

    /// <summary>
    /// URL foto profil dari provider OAuth (Google/Facebook) — provider-agnostic,
    /// disimpan apa adanya dari profil terverifikasi. Null untuk user
    /// email/password. Kolom nullable: user lama tetap NULL.
    /// </summary>
    public string? AvatarUrl { get; set; }
    
    public ICollection<TopupOrder> Orders { get; set; } = new List<TopupOrder>();
}