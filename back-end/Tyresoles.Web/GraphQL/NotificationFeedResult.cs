using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Web.GraphQL;

/// <summary>
/// Notifications list plus server UTC "now" so the client can show relative times
/// against the same clock that wrote <see cref="Notification.CreatedAt"/> (avoids
/// wrong "hours ago" when the browser clock and API host disagree).
/// </summary>
public sealed class NotificationFeedResult
{
    public IReadOnlyList<Notification> Notifications { get; init; } = Array.Empty<Notification>();
    public DateTime ServerTimeUtc { get; init; }
}
