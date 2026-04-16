using HotChocolate;

namespace Tyresoles.Web.GraphQL;

public class Subscription
{
    [Subscribe]
    [Topic("OnNotification_{userId}")]
    public NotificationPushPayload OnNotification(string userId, [EventMessage] NotificationPushPayload payload)
    {
        return payload;
    }
}
