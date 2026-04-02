using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar;

public interface INotificationPublisher
{
    Task PublishNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
}
