using HotChocolate.Subscriptions;
using Tyresoles.Data.Features.Calendar;
using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Web.GraphQL;

public class GraphQLNotificationPublisher : INotificationPublisher
{
    private readonly ITopicEventSender _eventSender;

    public GraphQLNotificationPublisher(ITopicEventSender eventSender)
    {
        _eventSender = eventSender;
    }

    public async Task PublishNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        // Topic name is formatted as "OnNotification_{UserId}" for efficient filtering
        var topicName = $"OnNotification_{notification.UserId}";
        await _eventSender.SendAsync(topicName, notification, cancellationToken);
    }
}
