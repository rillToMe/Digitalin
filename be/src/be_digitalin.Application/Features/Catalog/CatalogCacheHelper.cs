using System.Text.Json;
using be_digitalin.Application.Ports;
using be_digitalin.Application.Serialization;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Games;

/// <summary>
/// Helper L2 cache Redis untuk data katalog (fail-open ke MySQL saat Redis tidak tersedia).
/// </summary>
public sealed class CatalogCacheHelper
{
    private readonly IApplicationDbContext _db;
    private readonly IRedisService? _redis;
    private readonly ILogger _logger;

    public CatalogCacheHelper(IApplicationDbContext db, IRedisService? redis, ILogger logger)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
    }

    private static TimeSpan CacheTtl(int minutes) =>
        TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(Random.Shared.Next(0, 31)));

    public async Task<List<GameMetadataResponse>> TryGetGameMetadataListAsync()
    {
        var cached = await TryGetCacheAsync<List<GameMetadataResponse>>(RedisCacheKeys.GameMetadata);
        if (cached is not null)
            return cached;

        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .OrderBy(m => m.Brand)
            .ToListAsync();

        var result = metadata
            .Select(MapMetadataResponse)
            .ToList();

        await TrySetCacheAsync(RedisCacheKeys.GameMetadata, result, 10);
        return result;
    }

    public async Task<T?> TryGetCacheAsync<T>(string key) where T : class
    {
        if (_redis is null) return null;

        try
        {
            var json = await _redis.GetAsync(key);
            return json is null ? null : JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET gagal untuk {CacheKey}; fallback ke database.", key);
            return null;
        }
    }

    public async Task TrySetCacheAsync<T>(string key, T value, int ttlMinutes)
    {
        if (_redis is null) return;

        try
        {
            await _redis.SetAsync(key, JsonSerializer.Serialize(value), CacheTtl(ttlMinutes));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET gagal untuk {CacheKey}; melewati cache.", key);
        }
    }

    public async Task TryInvalidateCacheAsync(params string[] keys)
    {
        if (_redis is null) return;

        foreach (var key in keys)
        {
            try
            {
                await _redis.DeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis DELETE gagal untuk {CacheKey}; cache akan expired sendiri.", key);
            }
        }
    }

    public static GameMetadataResponse MapMetadataResponse(GameMetadata metadata) =>
        new(
            metadata.Id,
            metadata.Brand,
            metadata.BrandSlug,
            metadata.ImageUrl,
            GameInputSchemaSerializer.Deserialize(metadata.InputSchemaJson),
            metadata.TargetTemplate,
            metadata.SupplierNote,
            GameMetadata.ParseCategories(metadata.Categories),
            metadata.UsernameCheckProductId);
}
