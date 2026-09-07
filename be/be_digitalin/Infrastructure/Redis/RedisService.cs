using be_digitalin.Application.Ports;
using StackExchange.Redis;

namespace be_digitalin.Infrastructure.Redis;

public class RedisService : IRedisService
{
    private readonly IDatabase _db;

    public RedisService(IConnectionMultiplexer multiplexer)
    {
        _db = multiplexer.GetDatabase();
    }

    public async Task SetAsync(string key, string value, TimeSpan expiry) =>
        await _db.StringSetAsync(key, value, expiry);

    public async Task<string?> GetAsync(string key)
    {
        var value = await _db.StringGetAsync(key);
        return value.IsNull ? null : value.ToString();
    }

    public async Task DeleteAsync(string key) =>
        await _db.KeyDeleteAsync(key);
}
