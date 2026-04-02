namespace Tyresoles.Data.Features.Calendar.Dto;

public class CreateEventInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? TimeZoneId { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }
    public int? Visibility { get; set; }
    public int? ShowAs { get; set; }
    public int? Status { get; set; }
    public int? EventTypeId { get; set; }
    public RecurrenceInput? Recurrence { get; set; }
    public List<EventTagInput>? Tags { get; set; }
    public List<ReminderInput>? Reminders { get; set; }
    public List<AttendeeInput>? Attendees { get; set; }
    public List<CalendarTaskInput>? Tasks { get; set; }
}

public class CalendarTaskInput
{
    public Guid? Id { get; set; }
    public Guid? ParentTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public List<CalendarTaskInput>? SubTasks { get; set; }
}

public class AttendeeInput
{
    public string UserId { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}
