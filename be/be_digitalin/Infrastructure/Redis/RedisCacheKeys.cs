namespace be_digitalin.Infrastructure.Redis;

/// <summary>
/// SSOT semua cache key Redis aplikasi. Versioned agar invalidasi massal
/// cukup dengan bump versi.
/// </summary>
public static class RedisCacheKeys
{
    public const string PopularGames = "digitalin:cache:popular-games:v1";
    public const string GameCategories = "digitalin:cache:game-categories:v1";
    public const string GameCategoryRegistry = "digitalin:cache:game-category-registry:v1";
    public const string GameMetadata = "digitalin:cache:game-metadata:v1";
}
