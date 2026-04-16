using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Tyresoles.Data.Features.Admin.Session;

/// <summary>
/// Redis-based session store for multi-instance support and persistence across restarts.
/// Uses a combination of IDistributedCache for simple operations and IConnectionMultiplexer for complex queries (List).
/// </summary>
public sealed class RedisSessionStore : ISessionStore
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    /// <summary>Must match <see cref="RedisCacheOptions.InstanceName"/> — IDistributedCache prefixes all keys with this.</summary>
    private readonly string _distributedCacheKeyPrefix;
    private const string KeyPrefix = "session:";
    private const string UserIndexPrefix = "user_sessions:";

    public RedisSessionStore(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        IOptions<RedisCacheOptions> redisCacheOptions)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _distributedCacheKeyPrefix = redisCacheOptions.Value.InstanceName ?? "";
    }

    public async Task<SessionInfo> CreateAsync(SessionInfo session, CancellationToken cancellationToken = default)
    {
        var key = GetKey(session.SessionId);
        var json = JsonSerializer.Serialize(session);
        var expiry = session.ExpiresAtUtc - DateTime.UtcNow;

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = session.ExpiresAtUtc
        };

        await _cache.SetStringAsync(key, json, options, cancellationToken);
        
        // Index by User ID for ListByUser/RemoveByUser
        var db = _redis.GetDatabase();
        var userKey = GetUserKey(session.UserId);
        await db.SetAddAsync(userKey, session.SessionId);
        await db.KeyExpireAsync(userKey, expiry > TimeSpan.Zero ? expiry : TimeSpan.FromHours(24));

        return session;
    }

    public async Task<SessionInfo?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var json = await _cache.GetStringAsync(GetKey(sessionId), cancellationToken);
        if (string.IsNullOrEmpty(json)) return null;

        var session = JsonSerializer.Deserialize<SessionInfo>(json);
        if (session != null && session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await RemoveSessionDataAsync(sessionId, session, cancellationToken);
            return null;
        }
        return session;
    }

    public async Task<IReadOnlyList<SessionInfo>> ListAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var sessions = new List<SessionInfo>();

        if (!string.IsNullOrEmpty(userId))
        {
            var userKey = GetUserKey(userId);
            var sessionIds = await db.SetMembersAsync(userKey);
            foreach (var id in sessionIds)
            {
                var s = await GetAsync(id!, cancellationToken);
                if (s != null) sessions.Add(s);
                else await db.SetRemoveAsync(userKey, id); // Cleanup stale index
            }
        }
        else
        {
            // Scanning all sessions (O(N) - use with caution if session count is massive).
            // Keys written via IDistributedCache use InstanceName + logical key (e.g. Tyresoles:session:*), not session:* alone.
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            var pattern = _distributedCacheKeyPrefix + KeyPrefix + "*";
            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                // Must use IDistributedCache, not db.StringGet: RedisCache may store values as HASH (not STRING).
                // StringGet on those keys throws WRONGTYPE.
                var keyStr = key.ToString();
                if (keyStr.Length <= _distributedCacheKeyPrefix.Length)
                    continue;
                var logicalKey = keyStr[_distributedCacheKeyPrefix.Length..];
                var json = await _cache.GetStringAsync(logicalKey, cancellationToken);
                if (string.IsNullOrEmpty(json))
                    continue;
                var s = JsonSerializer.Deserialize<SessionInfo>(json);
                if (s == null)
                    continue;
                if (s.ExpiresAtUtc > DateTime.UtcNow)
                {
                    sessions.Add(s);
                    continue;
                }

                var sid = logicalKey.StartsWith(KeyPrefix, StringComparison.Ordinal)
                    ? logicalKey[KeyPrefix.Length..]
                    : logicalKey;
                await RemoveSessionDataAsync(sid, s, cancellationToken);
            }
        }

        return sessions.OrderByDescending(s => s.CreatedAtUtc).ToList();
    }

    public async Task<bool> RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var json = await _cache.GetStringAsync(GetKey(sessionId), cancellationToken);
        if (string.IsNullOrEmpty(json))
            return false;

        var session = JsonSerializer.Deserialize<SessionInfo>(json);
        await RemoveSessionDataAsync(sessionId, session, cancellationToken);
        return true;
    }

    /// <summary>Removes cache entry and user index without re-entering <see cref="GetAsync"/> (avoids recursion when the session is expired).</summary>
    private async Task RemoveSessionDataAsync(string sessionId, SessionInfo? session, CancellationToken cancellationToken)
    {
        if (session != null)
        {
            var db = _redis.GetDatabase();
            await db.SetRemoveAsync(GetUserKey(session.UserId), sessionId);
        }

        await _cache.RemoveAsync(GetKey(sessionId), cancellationToken);
    }

    public async Task<int> RemoveByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var userKey = GetUserKey(userId);
        var sessionIds = await db.SetMembersAsync(userKey);
        int count = 0;

        foreach (var id in sessionIds)
        {
            if (await RemoveAsync(id!, cancellationToken))
                count++;
        }
        
        await db.KeyDeleteAsync(userKey);
        return count;
    }

    private static string GetKey(string id) => KeyPrefix + id;
    private static string GetUserKey(string userId) => UserIndexPrefix + userId.ToLowerInvariant();
}
