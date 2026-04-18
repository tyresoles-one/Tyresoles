using System.Net.Mime;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.RemoteAssist;
using Tyresoles.Web.Features.RemoteAssist;

namespace Tyresoles.Web.Controllers;

[ApiController]
[Route("api/remote-assist")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
[EnableRateLimiting("RemoteAssist")]
public sealed class RemoteAssistController : ControllerBase
{
    private readonly IRemoteAssistService _assistService;
    private readonly IOptions<RemoteAssistIceOptions> _iceOptions;
    private readonly IOptions<RemoteAssistOptions> _assistOptions;
    private readonly ILogger<RemoteAssistController> _logger;

    public RemoteAssistController(
        IRemoteAssistService assistService,
        IOptions<RemoteAssistIceOptions> iceOptions,
        IOptions<RemoteAssistOptions> assistOptions,
        ILogger<RemoteAssistController> logger)
    {
        _assistService = assistService;
        _iceOptions = iceOptions;
        _assistOptions = assistOptions;
        _logger = logger;
    }

    /// <summary>STUN/TURN URLs for WebRTC (no secrets in response beyond configured static TURN credentials).</summary>
    [HttpGet("ice")]
    public ActionResult<IceServersResponse> GetIceServers()
    {
        var servers = _iceOptions.Value.IceServers
            .Where(s => !string.IsNullOrWhiteSpace(s.Urls))
            .Select(s => new IceServerDto
            {
                Urls = s.Urls,
                Username = s.Username,
                Credential = s.Credential
            })
            .ToList();
        return Ok(new IceServersResponse { IceServers = servers });
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<CreateSessionResponse>> CreateSession([FromBody] CreateSessionRequest? body, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _assistService.CreateSessionAsync(userId, body?.HostDisplayName, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("RemoteAssist session {SessionId} created by {UserId}", result.SessionId, userId);
        return Ok(new CreateSessionResponse
        {
            SessionId = result.SessionId,
            JoinCode = result.JoinCode,
            ExpiresAtUtc = result.ExpiresAtUtc,
            WebSocketPath = RemoteAssistWebSocketEndpoint.Path
        });
    }

    [HttpPost("sessions/join")]
    public async Task<ActionResult<JoinSessionResponse>> JoinSession([FromBody] JoinSessionRequest body, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.JoinCode))
            return BadRequest(new { error = "JoinCode is required." });

        var result = await _assistService.JoinSessionAsync(body.JoinCode.Trim(), userId, body.ViewerDisplayName, cancellationToken).ConfigureAwait(false);
        if (result is null)
            return NotFound(new { error = "Invalid or expired join code, or session full." });

        _logger.LogInformation("RemoteAssist session {SessionId} joined by viewer {UserId}", result.SessionId, userId);
        return Ok(new JoinSessionResponse
        {
            SessionId = result.SessionId,
            HostUserId = result.HostUserId,
            ExpiresAtUtc = result.ExpiresAtUtc,
            WebSocketPath = RemoteAssistWebSocketEndpoint.Path
        });
    }

    /// <summary>Active (non-ended, non-expired) assist sessions. Assist admins only (see RemoteAssist:AssistAdmin* config).</summary>
    [HttpGet("sessions/active")]
    public async Task<ActionResult<IReadOnlyList<ActiveSessionListItemResponse>>> ListActiveSessions(CancellationToken cancellationToken)
    {
        if (!IsAssistAdmin(User))
            return Forbid();
        var rows = await _assistService.ListActiveSessionsAsync(cancellationToken).ConfigureAwait(false);
        var list = rows.Select(r => new ActiveSessionListItemResponse
        {
            SessionId = r.SessionId,
            JoinCode = r.JoinCode,
            HostUserId = r.HostUserId,
            HostDisplayName = r.HostDisplayName,
            Status = r.Status,
            ViewerUserId = r.ViewerUserId,
            ExpiresAtUtc = r.ExpiresAtUtc,
            CreatedAtUtc = r.CreatedAtUtc,
        }).ToList();
        return Ok(list);
    }

    /// <summary>Join as viewer by session id without a join code (replaces existing viewer). Assist admins only.</summary>
    [HttpPost("sessions/{sessionId:guid}/admin-join")]
    public async Task<ActionResult<JoinSessionResponse>> AdminJoinSession(
        Guid sessionId,
        [FromBody] AdminJoinSessionRequest? body,
        CancellationToken cancellationToken)
    {
        if (!IsAssistAdmin(User))
            return Forbid();
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        var result = await _assistService.AdminJoinSessionAsync(sessionId, userId, body?.ViewerDisplayName, cancellationToken).ConfigureAwait(false);
        if (result is null)
            return NotFound(new { error = "Session not found, ended, expired, or you are the host of this session." });
        _logger.LogInformation("RemoteAssist session {SessionId} joined by assist admin {UserId}", sessionId, userId);
        return Ok(new JoinSessionResponse
        {
            SessionId = result.SessionId,
            HostUserId = result.HostUserId,
            ExpiresAtUtc = result.ExpiresAtUtc,
            WebSocketPath = RemoteAssistWebSocketEndpoint.Path
        });
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<ActionResult<SessionStatusResponse>> GetSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var session = await _assistService.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
            return NotFound();

        if (!IsAssistAdmin(User) &&
            !session.HostUserId.Equals(userId, StringComparison.OrdinalIgnoreCase) &&
            !(session.ViewerUserId?.Equals(userId, StringComparison.OrdinalIgnoreCase) ?? false))
            return Forbid();

        return Ok(new SessionStatusResponse
        {
            SessionId = session.Id,
            JoinCode = session.JoinCode,
            Status = session.Status.ToString(),
            HostUserId = session.HostUserId,
            ViewerUserId = session.ViewerUserId,
            ExpiresAtUtc = session.ExpiresAtUtc,
            EndedAtUtc = session.EndedAtUtc,
            ControlApprovedAtUtc = session.ControlApprovedAtUtc
        });
    }

    /// <summary>Host approves or revokes remote control relay (viewer mouse/keyboard to host).</summary>
    [HttpPost("sessions/{sessionId:guid}/remote-control")]
    public async Task<IActionResult> SetRemoteControl(Guid sessionId, [FromBody] RemoteControlRequest body, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var ok = await _assistService.SetRemoteControlApprovedAsync(sessionId, userId, body.Enabled, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return NotFound(new { error = "Session not found or not allowed." });

        _logger.LogInformation("RemoteAssist session {SessionId} remote control set to {Enabled} by host", sessionId, body.Enabled);
        return Ok(new { enabled = body.Enabled });
    }

    [HttpPost("sessions/{sessionId:guid}/end")]
    public async Task<IActionResult> EndSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var ok = await _assistService.EndSessionAsync(sessionId, userId, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return NotFound(new { error = "Session not found or not allowed." });

        return Ok(new { ended = true });
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

    private bool IsAssistAdmin(ClaimsPrincipal user)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return false;
        var opts = _assistOptions.Value;
        foreach (var id in opts.AssistAdminUserIds)
        {
            if (!string.IsNullOrWhiteSpace(id) && id.Equals(userId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        var userType = user.FindFirstValue("userType") ?? "";
        foreach (var t in opts.AssistAdminUserTypes)
        {
            if (!string.IsNullOrWhiteSpace(t) && t.Equals(userType, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public sealed class CreateSessionRequest
{
    public string? HostDisplayName { get; set; }
}

public sealed class JoinSessionRequest
{
    public string JoinCode { get; set; } = "";
    public string? ViewerDisplayName { get; set; }
}

public sealed class CreateSessionResponse
{
    public Guid SessionId { get; set; }
    public string JoinCode { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string WebSocketPath { get; set; } = "";
}

public sealed class JoinSessionResponse
{
    public Guid SessionId { get; set; }
    public string HostUserId { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string WebSocketPath { get; set; } = "";
}

public sealed class SessionStatusResponse
{
    public Guid SessionId { get; set; }
    public string JoinCode { get; set; } = "";
    public string Status { get; set; } = "";
    public string HostUserId { get; set; } = "";
    public string? ViewerUserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    public DateTime? ControlApprovedAtUtc { get; set; }
}

public sealed class RemoteControlRequest
{
    public bool Enabled { get; set; }
}

public sealed class AdminJoinSessionRequest
{
    public string? ViewerDisplayName { get; set; }
}

public sealed class ActiveSessionListItemResponse
{
    public Guid SessionId { get; set; }
    public string JoinCode { get; set; } = "";
    public string HostUserId { get; set; } = "";
    public string? HostDisplayName { get; set; }
    public string Status { get; set; } = "";
    public string? ViewerUserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class IceServersResponse
{
    public List<IceServerDto> IceServers { get; set; } = new();
}

public sealed class IceServerDto
{
    public string Urls { get; set; } = "";
    public string? Username { get; set; }
    public string? Credential { get; set; }
}
