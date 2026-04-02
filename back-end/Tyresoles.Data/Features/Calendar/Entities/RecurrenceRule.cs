namespace Tyresoles.Data.Features.Calendar.Entities;

public enum RecurrenceFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}

public class RecurrenceRule
{
    public Guid Id { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    /// <summary>JSON array of weekday numbers (0=Sun..6=Sat) for Weekly; or null.</summary>
    public string? DaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public int? MonthOfYear { get; set; }
    public DateOnly? EndByDate { get; set; }
    public int? OccurrenceCount { get; set; }
    public string? RRule { get; set; }

    public ICollection<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
}
