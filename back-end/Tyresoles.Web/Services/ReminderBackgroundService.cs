using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.Calendar;
using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Web.Services;

public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReminderBackgroundService> _logger;

    public ReminderBackgroundService(IServiceProvider serviceProvider, ILogger<ReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reminder Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing reminders.");
            }

            // Poll every 1 minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        
        // Find reminders that are due and not yet sent
        var dueReminders = await db.Reminders
            .Include(r => r.Event)
            .Where(r => !r.IsSent && r.RemindAtUtc <= now)
            .Where(r => r.SnoozeUntilUtc == null || r.SnoozeUntilUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var reminder in dueReminders)
        {
            if (reminder.Event == null || reminder.Event.IsDeleted)
            {
                reminder.IsSent = true; // Mark as sent if event is gone
                continue;
            }

            _logger.LogInformation("Sending reminder for event: {EventTitle} to user: {UserId}", reminder.Event.Title, reminder.Event.OwnerUserId);

            await notificationService.SendNotificationAsync(
                reminder.Event.OwnerUserId,
                "Event Reminder",
                $"Reminder: {reminder.Event.Title} starts at {reminder.Event.StartUtc:t}",
                NotificationType.Info,
                $"/calendar/event/{reminder.Event.Id}",
                cancellationToken
            );

            reminder.IsSent = true;
            reminder.SentAt = DateTime.UtcNow;
        }

        if (dueReminders.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
