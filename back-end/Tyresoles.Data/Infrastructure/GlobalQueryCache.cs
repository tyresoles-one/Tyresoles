using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Tyresoles.Data.Infrastructure;

/// <summary>
/// Global, distributed query cache powered by Redis.
/// Used for caching results across requests and application instances.
/// </summary>
public sealed class GlobalQueryCache
{
    private readonly IDistributedCache _cache;
    private const string GlobalPrefix = "qcache:";

    public GlobalQueryCache(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Gets a cached value or computes and caches it.
    /// Default TTL is 10 minutes if not specified.
    /// </summary>
    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        var cacheKey = GlobalPrefix + key;

        try
        {
            var json = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(json))
            {
                return JsonSerializer.Deserialize<T>(json)!;
            }

            var result = await factory();
            if (result != null)
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromMinutes(10)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), options);
            }

            return result!;
        }
        catch (RedisConnectionException)
        {
            var result = await factory();
            return result!;
        }
        catch (RedisTimeoutException)
        {
            var result = await factory();
            return result!;
        }
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(GlobalPrefix + key);
    }
}
