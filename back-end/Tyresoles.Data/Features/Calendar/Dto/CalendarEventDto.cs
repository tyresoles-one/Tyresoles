using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar.Dto;

public class EventTagDto
{
    public EventTagType TagType { get; set; }
    public string TagKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public class ReminderDto
{
    public Guid Id { get; set; }
    public DateTime RemindAtUtc { get; set; }
    public ReminderChannel Channel { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SnoozeUntilUtc { get; set; }
}

public class EventAttendeeDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int Response { get; set; }
    public bool IsRequired { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class CalendarShareDto
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string SharedWithUserId { get; set; } = string.Empty;
    public int Permission { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CalendarTaskDto
{
    public Guid Id { get; set; }
    public Guid? ParentTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public List<CalendarTaskDto> SubTasks { get; set; } = new();
}

public class CalendarEventDto
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public int? EventTypeId { get; set; }
    public string? EventTypeName { get; set; }
    public string? EventTypeColor { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? TimeZoneId { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }
    public int Visibility { get; set; }
    public int ShowAs { get; set; }
    public int Status { get; set; }
    public Guid? RecurrenceRuleId { get; set; }
    public Guid? ParentEventId { get; set; }
    public DateTime? ExceptionOccurrenceStartUtc { get; set; }
    public List<EventTagDto> Tags { get; set; } = new();
    public List<ReminderDto> Reminders { get; set; } = new();
    public List<EventAttendeeDto> Attendees { get; set; } = new();
    public List<CalendarTaskDto> Tasks { get; set; } = new();
    public RecurrenceDto? Recurrence { get; set; }
}
