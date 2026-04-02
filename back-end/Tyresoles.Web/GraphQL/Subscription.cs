using HotChocolate;
using HotChocolate.Types;
using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Web.GraphQL;

public class Subscription
{
    [Subscribe]
    [Topic("OnNotification_{userId}")]
    public Notification OnNotification(string userId, [EventMessage] Notification notification)
    {
        return notification;
    }
}
