using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.Calendar;
using Tyresoles.Data.Features.RemoteAssist.Entities;

namespace Tyresoles.Data.Features.RemoteAssist;

public sealed class RemoteAssistService : IRemoteAssistService
{
    private readonly CalendarDbContext _db;
    private readonly RemoteAssistOptions _options;
    private readonly IRemoteAssistControlNotifier _controlNotifier;

    public RemoteAssistService(
        CalendarDbContext db,
        IOptions<RemoteAssistOptions> options,
        IRemoteAssistControlNotifier controlNotifier)
    {
        _db = db;
        _options = options.Value;
        _controlNotifier = controlNotifier;
    }

    public async Task<CreateRemoteAssistSessionResult> CreateSessionAsync(
        string hostUserId,
        string? hostDisplayName,
        CancellationToken cancellationToken = default)
    {
        var joinCode = await GenerateUniqueJoinCodeAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var timeout = TimeSpan.FromMinutes(Math.Clamp(_options.SessionTimeoutMinutes, 5, 480));
        var entity = new RemoteAssistSession
        {
            Id = Guid.NewGuid(),
            JoinCode = joinCode,
            HostUserId = hostUserId,
            HostDisplayName = hostDisplayName,
            Status = RemoteAssistSessionStatus.Pending,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(timeout)
        };
        _db.RemoteAssistSessions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CreateRemoteAssistSessionResult
        {
            SessionId = entity.Id,
            JoinCode = entity.JoinCode,
            ExpiresAtUtc = entity.ExpiresAtUtc
        };
    }

    public async Task<JoinRemoteAssistSessionResult?> JoinSessionAsync(
        string joinCode,
        string viewerUserId,
        string? viewerDisplayName,
        CancellationToken cancellationToken = default)
    {
        var code = joinCode.Trim().ToUpperInvariant();
        var session = await _db.RemoteAssistSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.JoinCode == code, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
            return null;
        if (session.Status == RemoteAssistSessionStatus.Ended || session.ExpiresAtUtc < DateTime.UtcNow)
            return null;

        var tracked = await _db.RemoteAssistSessions
            .FirstOrDefaultAsync(s => s.Id == session.Id, cancellationToken)
            .ConfigureAwait(false);
        if (tracked is null)
            return null;
        if (tracked.Status == RemoteAssistSessionStatus.Ended || tracked.ExpiresAtUtc < DateTime.UtcNow)
            return null;

        if (string.IsNullOrEmpty(tracked.ViewerUserId))
        {
            tracked.ViewerUserId = viewerUserId;
            tracked.ViewerDisplayName = viewerDisplayName;
            tracked.Status = RemoteAssistSessionStatus.Active;
        }
        else if (!tracked.ViewerUserId.Equals(viewerUserId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new JoinRemoteAssistSessionResult
        {
            SessionId = tracked.Id,
            HostUserId = tracked.HostUserId,
            ExpiresAtUtc = tracked.ExpiresAtUtc
        };
    }

    public async Task<JoinRemoteAssistSessionResult?> AdminJoinSessionAsync(
        Guid sessionId,
        string adminUserId,
        string? viewerDisplayName,
        CancellationToken cancellationToken = default)
    {
        var tracked = await _db.RemoteAssistSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (tracked is null)
            return null;
        if (tracked.Status == RemoteAssistSessionStatus.Ended || tracked.ExpiresAtUtc < DateTime.UtcNow)
            return null;
        if (tracked.HostUserId.Equals(adminUserId, StringComparison.OrdinalIgnoreCase))
            return null;

        tracked.ViewerUserId = adminUserId;
        tracked.ViewerDisplayName = viewerDisplayName;
        tracked.Status = RemoteAssistSessionStatus.Active;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new JoinRemoteAssistSessionResult
        {
            SessionId = tracked.Id,
            HostUserId = tracked.HostUserId,
            ExpiresAtUtc = tracked.ExpiresAtUtc
        };
    }

    public async Task<IReadOnlyList<ActiveRemoteAssistSessionDto>> ListActiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.RemoteAssistSessions.AsNoTracking()
            .Where(s => s.Status != RemoteAssistSessionStatus.Ended && s.ExpiresAtUtc >= now)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(s => new ActiveRemoteAssistSessionDto
        {
            SessionId = s.Id,
            JoinCode = s.JoinCode,
            HostUserId = s.HostUserId,
            HostDisplayName = s.HostDisplayName,
            Status = s.Status.ToString(),
            ViewerUserId = s.ViewerUserId,
            ExpiresAtUtc = s.ExpiresAtUtc,
            CreatedAtUtc = s.CreatedAtUtc,
        }).ToList();
    }

    public Task<RemoteAssistSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _db.RemoteAssistSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task<bool> EndSessionAsync(Guid sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var tracked = await _db.RemoteAssistSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (tracked is null || tracked.Status == RemoteAssistSessionStatus.Ended)
            return false;
        if (!tracked.HostUserId.Equals(userId, StringComparison.OrdinalIgnoreCase) &&
            !(tracked.ViewerUserId?.Equals(userId, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        tracked.Status = RemoteAssistSessionStatus.Ended;
        tracked.EndedAtUtc = DateTime.UtcNow;
        tracked.EndedByUserId = userId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _controlNotifier.ClearSession(sessionId);
        return true;
    }

    public async Task<bool> SetRemoteControlApprovedAsync(
        Guid sessionId,
        string hostUserId,
        bool approved,
        CancellationToken cancellationToken = default)
    {
        var tracked = await _db.RemoteAssistSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (tracked is null || tracked.Status == RemoteAssistSessionStatus.Ended || tracked.ExpiresAtUtc < DateTime.UtcNow)
            return false;
        if (!tracked.HostUserId.Equals(hostUserId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (approved)
        {
            tracked.ControlApprovedAtUtc = DateTime.UtcNow;
            _controlNotifier.SetControlRelay(sessionId, true);
        }
        else
        {
            tracked.ControlApprovedAtUtc = null;
            _controlNotifier.SetControlRelay(sessionId, false);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> CanUserAccessSessionAsync(Guid sessionId, string userId, bool asHost, CancellationToken cancellationToken = default)
    {
        var session = await _db.RemoteAssistSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null || session.Status == RemoteAssistSessionStatus.Ended || session.ExpiresAtUtc < DateTime.UtcNow)
            return false;
        if (asHost)
            return session.HostUserId.Equals(userId, StringComparison.OrdinalIgnoreCase);
        return session.ViewerUserId?.Equals(userId, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private async Task<string> GenerateUniqueJoinCodeAsync(CancellationToken cancellationToken)
    {
        var len = Math.Clamp(_options.JoinCodeLength, 6, 12);
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var bytes = new byte[len];
            RandomNumberGenerator.Fill(bytes);
            var chars = new char[len];
            for (var i = 0; i < len; i++)
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            var code = new string(chars);
            var exists = await _db.RemoteAssistSessions.AnyAsync(s => s.JoinCode == code, cancellationToken).ConfigureAwait(false);
            if (!exists)
                return code;
        }

        return Guid.NewGuid().ToString("N")[..len].ToUpperInvariant();
    }
}
