namespace Tyresoles.Data.Features.Calendar.Entities;

public enum ReminderChannel
{
    InApp = 0,
    Email = 1,
    Push = 2
}

public class Reminder
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime RemindAtUtc { get; set; }
    public ReminderChannel Channel { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? SnoozeUntilUtc { get; set; }

    public CalendarEvent Event { get; set; } = null!;
}
