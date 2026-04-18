using Tyresoles.Data.Features.RemoteAssist.Entities;

namespace Tyresoles.Data.Features.RemoteAssist;

public interface IRemoteAssistService
{
    Task<CreateRemoteAssistSessionResult> CreateSessionAsync(
        string hostUserId,
        string? hostDisplayName,
        CancellationToken cancellationToken = default);

    Task<JoinRemoteAssistSessionResult?> JoinSessionAsync(
        string joinCode,
        string viewerUserId,
        string? viewerDisplayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Privileged join by session id (overwrites existing viewer if any). Host cannot use this for their own session.
    /// </summary>
    Task<JoinRemoteAssistSessionResult?> AdminJoinSessionAsync(
        Guid sessionId,
        string adminUserId,
        string? viewerDisplayName,
        CancellationToken cancellationToken = default);

    /// <summary>Non-ended, non-expired assist sessions (for admin dashboard).</summary>
    Task<IReadOnlyList<ActiveRemoteAssistSessionDto>> ListActiveSessionsAsync(CancellationToken cancellationToken = default);

    Task<RemoteAssistSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<bool> EndSessionAsync(Guid sessionId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Returns true if user may open a signaling connection for this session.</summary>
    Task<bool> CanUserAccessSessionAsync(Guid sessionId, string userId, bool asHost, CancellationToken cancellationToken = default);

    /// <summary>Host approves or revokes remote control relay for the session.</summary>
    Task<bool> SetRemoteControlApprovedAsync(Guid sessionId, string hostUserId, bool approved, CancellationToken cancellationToken = default);
}

public sealed class CreateRemoteAssistSessionResult
{
    public required Guid SessionId { get; init; }
    public required string JoinCode { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}

public sealed class JoinRemoteAssistSessionResult
{
    public required Guid SessionId { get; init; }
    public required string HostUserId { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}

public sealed class ActiveRemoteAssistSessionDto
{
    public required Guid SessionId { get; init; }
    public required string JoinCode { get; init; }
    public required string HostUserId { get; init; }
    public string? HostDisplayName { get; init; }
    public string Status { get; init; } = "";
    public string? ViewerUserId { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
