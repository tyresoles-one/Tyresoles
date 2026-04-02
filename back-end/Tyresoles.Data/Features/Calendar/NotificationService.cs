using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar;

public sealed class NotificationService : INotificationService
{
    private readonly CalendarDbContext _db;
    private readonly INotificationPublisher _publisher;

    public NotificationService(CalendarDbContext db, INotificationPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<Notification> SendNotificationAsync(string userId, string title, string message, NotificationType type, string? link = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Link = link,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        await _publisher.PublishNotificationAsync(notification, cancellationToken);

        return notification;
    }

    public async Task<IReadOnlyList<Notification>> GetMyNotificationsAsync(string userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, string userId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification == null) return false;

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);
        return true;
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }
}
