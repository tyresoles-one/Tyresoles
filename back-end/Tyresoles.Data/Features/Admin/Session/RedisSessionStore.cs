using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
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
    private const string KeyPrefix = "session:";
    private const string UserIndexPrefix = "user_sessions:";

    public RedisSessionStore(IDistributedCache cache, IConnectionMultiplexer redis)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
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
            await RemoveAsync(sessionId, cancellationToken);
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
            // Scanning all sessions (O(N) - use with caution if session count is massive)
            // In a real high-scale system, we'd use a Global Session Index set.
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            await foreach (var key in server.KeysAsync(pattern: KeyPrefix + "*"))
            {
                var json = await db.StringGetAsync(key);
                if (!json.IsNull)
                {
                    var s = JsonSerializer.Deserialize<SessionInfo>((string)json!);
                    if (s != null && s.ExpiresAtUtc > DateTime.UtcNow) sessions.Add(s);
                }
            }
        }

        return sessions.OrderByDescending(s => s.CreatedAtUtc).ToList();
    }

    public async Task<bool> RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetAsync(sessionId, cancellationToken);
        if (session != null)
        {
            var db = _redis.GetDatabase();
            await db.SetRemoveAsync(GetUserKey(session.UserId), sessionId);
        }
        
        await _cache.RemoveAsync(GetKey(sessionId), cancellationToken);
        return session != null;
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
