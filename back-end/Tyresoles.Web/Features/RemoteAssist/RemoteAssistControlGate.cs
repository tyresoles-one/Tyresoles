using System.Collections.Concurrent;
using Tyresoles.Data.Features.RemoteAssist;

namespace Tyresoles.Web.Features.RemoteAssist;

/// <summary>Tracks which sessions may relay <c>control</c> messages from viewer to host (after host approval via API).</summary>
public sealed class RemoteAssistControlGate : IRemoteAssistControlNotifier
{
    private readonly ConcurrentDictionary<Guid, bool> _relay = new();

    public void SetControlRelay(Guid sessionId, bool allowed)
    {
        if (allowed)
            _relay[sessionId] = true;
        else
            _relay.TryRemove(sessionId, out _);
    }

    public void ClearSession(Guid sessionId) => _relay.TryRemove(sessionId, out _);

    public bool IsRelayAllowed(Guid sessionId) =>
        _relay.TryGetValue(sessionId, out var v) && v;
}
