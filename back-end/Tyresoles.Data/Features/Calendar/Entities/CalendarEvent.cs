namespace Tyresoles.Data.Features.Calendar.Entities;

public class CalendarEvent
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public Guid? CalendarId { get; set; }
    public int? EventTypeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? TimeZoneId { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }
    public EventVisibility Visibility { get; set; } = EventVisibility.Default;
    public EventShowAs ShowAs { get; set; } = EventShowAs.Busy;
    public EventStatus Status { get; set; } = EventStatus.Confirmed;
    public Guid? RecurrenceRuleId { get; set; }
    public Guid? ParentEventId { get; set; }
    /// <summary>For exception events: the occurrence date this exception replaces (UTC date of start).</summary>
    public DateTime? ExceptionOccurrenceStartUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public bool IsDeleted { get; set; }

    public RecurrenceRule? RecurrenceRule { get; set; }
    public EventType? EventType { get; set; }
    public CalendarEvent? ParentEvent { get; set; }
    public ICollection<CalendarEvent> ExceptionEvents { get; set; } = new List<CalendarEvent>();
    public ICollection<EventTag> Tags { get; set; } = new List<EventTag>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    public ICollection<EventAttendee> Attendees { get; set; } = new List<EventAttendee>();
    public ICollection<CalendarTask> Tasks { get; set; } = new List<CalendarTask>();
}
