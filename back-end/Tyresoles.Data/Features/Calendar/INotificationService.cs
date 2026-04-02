using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar;

public interface INotificationService
{
    Task<Notification> SendNotificationAsync(string userId, string title, string message, NotificationType type, string? link = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetMyNotificationsAsync(string userId, int limit = 50, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(Guid notificationId, string userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
}
