namespace be_digitalin.DTOs;

/// <summary>
/// Satu baris leaderboard pembeli (publik). Hanya data minimum yang aman —
/// tidak ada email/telepon/order number/id internal (CustomerId sengaja
/// tidak ikut ke response).
/// </summary>
public record LeaderboardEntryResponse(
    int Rank,
    string DisplayName,
    /// <summary>URL avatar OAuth (Customer.AvatarUrl); null = fallback ikon default.</summary>
    string? AvatarUrl,
    decimal TotalSpent);
