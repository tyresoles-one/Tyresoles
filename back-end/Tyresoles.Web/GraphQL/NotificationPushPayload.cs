using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Web.GraphQL;

/// <summary>Subscription payload: notification plus server UTC at publish time.</summary>
public sealed class NotificationPushPayload
{
    public Notification Notification { get; init; } = default!;
    public DateTime ServerTimeUtc { get; init; }
}
