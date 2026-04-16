using System.IdentityModel.Tokens.Jwt;
using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Tyresoles.Data.Features.Admin.Session;
using Tyresoles.Data.Features.RemoteAssist;

namespace Tyresoles.Web.Features.RemoteAssist;

public static class RemoteAssistWebSocketEndpoint
{
    public const string Path = "/ws/remote-assist";

    public static IEndpointRouteBuilder MapRemoteAssistWebSocket(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map(Path, HandleAsync);
        return endpoints;
    }

    private static async Task HandleAsync(
        HttpContext httpContext,
        RemoteAssistJwtValidator jwtValidator,
        ISessionStore sessionStore,
        IRemoteAssistService assistService,
        RemoteAssistControlGate controlGate,
        RemoteAssistSignalingHub hub,
        CancellationToken cancellationToken)
    {
        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new { error = "Expected WebSocket upgrade." }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var token = httpContext.Request.Query["access_token"].FirstOrDefault();
        var sessionIdStr = httpContext.Request.Query["sessionId"].FirstOrDefault();
        var role = httpContext.Request.Query["role"].FirstOrDefault();

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(sessionIdStr) ||
            !Guid.TryParse(sessionIdStr, out var sessionId) ||
            string.IsNullOrEmpty(role))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new { error = "Missing access_token, sessionId, or role." }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var isHost = role.Equals("host", StringComparison.OrdinalIgnoreCase);
        if (!isHost && !role.Equals("viewer", StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new { error = "role must be host or viewer." }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var principal = await jwtValidator.ValidateAsync(token, sessionStore, cancellationToken).ConfigureAwait(false);
        if (principal is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(userId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!await assistService.CanUserAccessSessionAsync(sessionId, userId, isHost, cancellationToken).ConfigureAwait(false))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var assistRow = await assistService.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (assistRow?.ControlApprovedAtUtc != null)
            controlGate.SetControlRelay(sessionId, true);

        using var socket = await httpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        if (!hub.TryRegister(sessionId, isHost, socket, out var regError))
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, regError ?? "Slot taken", cancellationToken).ConfigureAwait(false);
            return;
        }

        await hub.FlushPendingAsync(sessionId, isHost, socket, cancellationToken).ConfigureAwait(false);

        try
        {
            await hub.RelayLoopAsync(sessionId, isHost, socket, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            hub.Remove(sessionId, isHost, socket);
        }
    }
}
