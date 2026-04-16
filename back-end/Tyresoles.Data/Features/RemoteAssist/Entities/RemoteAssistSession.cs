namespace Tyresoles.Data.Features.RemoteAssist.Entities;

/// <summary>Screen-share assist session (WebRTC signaling + audit).</summary>
public sealed class RemoteAssistSession
{
    public Guid Id { get; set; }

    /// <summary>Short code for viewer join (e.g. 8 alphanumeric).</summary>
    public string JoinCode { get; set; } = string.Empty;

    public string HostUserId { get; set; } = string.Empty;

    /// <summary>Optional display name captured at create time for audit.</summary>
    public string? HostDisplayName { get; set; }

    /// <summary>First viewer to join (MVP: single viewer).</summary>
    public string? ViewerUserId { get; set; }

    public string? ViewerDisplayName { get; set; }

    public RemoteAssistSessionStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    /// <summary>Who ended the session (host or system).</summary>
    public string? EndedByUserId { get; set; }

    /// <summary>When the host approved remote mouse/keyboard control (audit + relay gate).</summary>
    public DateTime? ControlApprovedAtUtc { get; set; }
}

public enum RemoteAssistSessionStatus
{
    Pending = 0,
    Active = 1,
    Ended = 2
}
