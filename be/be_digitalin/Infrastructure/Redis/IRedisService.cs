namespace be_digitalin.Infrastructure.Redis;

/// <summary>
/// Akses Redis minimal untuk temporary state (OTP registrasi).
/// Jangan dikembangkan menjadi generic caching framework.
/// </summary>
public interface IRedisService
{
    Task SetAsync(string key, string value, TimeSpan expiry);

    /// <summary>Null bila key tidak ada / sudah expired.</summary>
    Task<string?> GetAsync(string key);

    Task DeleteAsync(string key);
}
