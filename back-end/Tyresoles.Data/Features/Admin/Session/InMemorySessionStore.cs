using System.Collections.Concurrent;

namespace Tyresoles.Data.Features.Admin.Session;

/// <summary>In-memory session store. Sessions are lost on app restart. Suitable for single instance.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    public Task<SessionInfo> CreateAsync(SessionInfo session, CancellationToken cancellationToken = default)
    {
        _sessions[session.SessionId] = session;
        return Task.FromResult(session);
    }

    public Task<SessionInfo?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var s))
            return Task.FromResult<SessionInfo?>(null);
        if (s.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _sessions.TryRemove(sessionId, out _);
            return Task.FromResult<SessionInfo?>(null);
        }
        return Task.FromResult<SessionInfo?>(s);
    }

    public Task<IReadOnlyList<SessionInfo>> ListAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var list = _sessions.Values
            .Where(s => s.ExpiresAtUtc > now && (userId == null || string.Equals(s.UserId, userId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<SessionInfo>>(list);
    }

    public Task<bool> RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sessions.TryRemove(sessionId, out _));
    }

    public Task<int> RemoveByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var toRemove = _sessions
            .Where(kv => string.Equals(kv.Value.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();
        var count = 0;
        foreach (var id in toRemove)
        {
            if (_sessions.TryRemove(id, out _))
                count++;
        }
        return Task.FromResult(count);
    }
}
