namespace be_digitalin.Application.Ports;

/// <summary>
/// Akses Redis minimal untuk temporary state (OTP registrasi) + cache read.
/// Dipindah dari Infrastructure/Redis (Fase 4): kontrak Application —
/// implementasi (RedisService) tetap di Infrastructure.
/// Jangan dikembangkan menjadi generic caching framework.
/// </summary>
public interface IRedisService
{
    Task SetAsync(string key, string value, TimeSpan expiry);

    /// <summary>Null bila key tidak ada / sudah expired.</summary>
    Task<string?> GetAsync(string key);

    Task DeleteAsync(string key);
}