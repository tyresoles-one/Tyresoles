namespace Tyresoles.Data.Features.Calendar.Dto;

public class UpdateEventInput
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public bool? IsAllDay { get; set; }
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
