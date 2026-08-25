using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace be_digitalin.Services;

public class AppSettingsService : IAppSettingsService
{
    // Cache TTL: 30 seconds. Admin changes propagate within this window.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private const string CachePrefix = "appsetting_";

    private readonly IDbContextFactory<DigitalinDbContext> _dbFactory;
    private readonly IMemoryCache _cache;

    public AppSettingsService(IDbContextFactory<DigitalinDbContext> dbFactory, IMemoryCache cache)
    {
        _dbFactory = dbFactory;
        _cache     = cache;
    }

    // ── Core get/set ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, string? defaultValue = null)
    {
        var cacheKey = CachePrefix + key;

        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached ?? defaultValue;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var record = await db.AppSettings.FindAsync(key);
        var value  = record?.Value;

        _cache.Set(cacheKey, value, CacheTtl);
        return value ?? defaultValue;
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var existing = await db.AppSettings.FindAsync(key);
        if (existing == null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }

        await db.SaveChangesAsync();

        // Invalidate cache so the next read reflects the new value immediately
        _cache.Remove(CachePrefix + key);
    }

    // ── Maintenance helpers ───────────────────────────────────────────────────
    /// <inheritdoc />
    public async Task<bool> IsMaintenanceEnabledAsync()
    {
        var raw = await GetAsync(AppConstants.SystemSettings.MaintenanceKeys.Enabled, "false");
        return bool.TryParse(raw, out var result) && result;
    }

    /// <inheritdoc />
    public async Task<MaintenanceConfig> GetMaintenanceConfigAsync()
    {
        // Use List<string> explicitly — EF Core on .NET 9+ misresolves string[].Contains()
        // to ReadOnlySpan<string>.Contains which cannot be used in LINQ-to-SQL expressions.
        var keys = new List<string>
        {
            AppConstants.SystemSettings.MaintenanceKeys.Enabled,
            AppConstants.SystemSettings.MaintenanceKeys.Title,
            AppConstants.SystemSettings.MaintenanceKeys.Message,
            AppConstants.SystemSettings.MaintenanceKeys.EstimatedFinish,
            AppConstants.SystemSettings.MaintenanceKeys.AllowAdmin,
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        bool ParseBool(string k, bool def) =>
            rows.TryGetValue(k, out var v) && bool.TryParse(v, out var b) ? b : def;

        string GetStr(string k, string def) =>
            rows.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

        return new MaintenanceConfig(
            Enabled:         ParseBool(AppConstants.SystemSettings.MaintenanceKeys.Enabled, false),
            Title:           GetStr(AppConstants.SystemSettings.MaintenanceKeys.Title,   "Website Sedang Maintenance"),
            Message:         GetStr(AppConstants.SystemSettings.MaintenanceKeys.Message, "Kami sedang melakukan peningkatan sistem. Silakan kembali beberapa saat lagi."),
            EstimatedFinish: rows.TryGetValue(AppConstants.SystemSettings.MaintenanceKeys.EstimatedFinish, out var ef)
                                 && !string.IsNullOrWhiteSpace(ef) ? ef : null,
            AllowAdmin:      ParseBool(AppConstants.SystemSettings.MaintenanceKeys.AllowAdmin, true)
        );
    }

    /// <inheritdoc />
    public async Task UpdateMaintenanceConfigAsync(MaintenanceConfig config)
    {
        await SetAsync(AppConstants.SystemSettings.MaintenanceKeys.Enabled,         config.Enabled.ToString().ToLower());
        await SetAsync(AppConstants.SystemSettings.MaintenanceKeys.Title,            config.Title);
        await SetAsync(AppConstants.SystemSettings.MaintenanceKeys.Message,          config.Message);
        await SetAsync(AppConstants.SystemSettings.MaintenanceKeys.EstimatedFinish,  config.EstimatedFinish ?? string.Empty);
        await SetAsync(AppConstants.SystemSettings.MaintenanceKeys.AllowAdmin,       config.AllowAdmin.ToString().ToLower());
    }
}
