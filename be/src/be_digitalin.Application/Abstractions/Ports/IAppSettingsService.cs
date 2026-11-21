namespace be_digitalin.Services.Settings;

/// <summary>
/// Generic AppSettings service — reads and writes key-value pairs from the
/// AppSettings table. Values are cached briefly to avoid hitting the DB on
/// every request that passes through the MaintenanceMiddleware.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Returns the value for the given key, or <paramref name="defaultValue"/>
    /// if the key does not exist in the database.
    /// </summary>
    Task<string?> GetAsync(string key, string? defaultValue = null);

    /// <summary>
    /// Upserts a key-value pair into AppSettings and invalidates its cache entry.
    /// </summary>
    Task SetAsync(string key, string value);

    /// <summary>
    /// Durasi expiry Snap Midtrans (menit) dari AppSettings. Nilai rusak, kosong,
    /// atau <= 0 jatuh kembali ke default
    /// <c>AppConstants.SystemSettings.MidtransExpiryDefaultMinutes</c> — satu
    /// SSOT untuk MidtransProvider (pembuatan Snap) dan OrderSweep (cutoff PENDING).
    /// </summary>
    Task<int> GetMidtransExpiryMinutesAsync();

    // ── Maintenance helpers ──────────────────────────────────────────────────

    /// <summary>Returns true when MAINTENANCE_MODE == "true" (case-insensitive).</summary>
    Task<bool> IsMaintenanceEnabledAsync();

    /// <summary>Returns a snapshot of all five maintenance-related settings.</summary>
    Task<MaintenanceConfig> GetMaintenanceConfigAsync();

    /// <summary>Persists all five maintenance-related settings in a single call.</summary>
    Task UpdateMaintenanceConfigAsync(MaintenanceConfig config);
}

/// <summary>
/// Value object holding all maintenance-related settings read from AppSettings.
/// </summary>
public record MaintenanceConfig(
    bool    Enabled,
    string  Title,
    string  Message,
    string? EstimatedFinish,
    bool    AllowAdmin
);
