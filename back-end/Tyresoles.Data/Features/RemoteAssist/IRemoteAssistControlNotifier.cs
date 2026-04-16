namespace Tyresoles.Data.Features.RemoteAssist;

/// <summary>Notifies the signaling layer when remote control relay is approved or a session ends.</summary>
public interface IRemoteAssistControlNotifier
{
    void SetControlRelay(Guid sessionId, bool allowed);

    void ClearSession(Guid sessionId);
}

/// <summary>No-op for tests or when Web gate is not used.</summary>
public sealed class NullRemoteAssistControlNotifier : IRemoteAssistControlNotifier
{
    public void SetControlRelay(Guid sessionId, bool allowed) { }

    public void ClearSession(Guid sessionId) { }
}
