namespace Tyresoles.Data.Features.Admin.Session;

/// <summary>Abstraction for storing and querying app sessions (e.g. for admin list/kill).</summary>
public interface ISessionStore
{
    /// <summary>Creates a new session and returns its info. Caller should set ExpiresAtUtc before calling.</summary>
    Task<SessionInfo> CreateAsync(SessionInfo session, CancellationToken cancellationToken = default);

    /// <summary>Gets a session by id, or null if not found or expired.</summary>
    Task<SessionInfo?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Returns all active sessions (optionally filter by userId).</summary>
    Task<IReadOnlyList<SessionInfo>> ListAsync(string? userId = null, CancellationToken cancellationToken = default);

    /// <summary>Removes a session by id. Returns true if it existed.</summary>
    Task<bool> RemoveAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Removes all sessions for the given user. Returns the number removed.</summary>
    Task<int> RemoveByUserAsync(string userId, CancellationToken cancellationToken = default);
}
