namespace Tyresoles.Data.Features.Calendar.Entities;

public enum AttendeeResponse
{
    None = 0,
    Accepted = 1,
    Declined = 2,
    Tentative = 3
}

public class EventAttendee
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public AttendeeResponse Response { get; set; }
    public bool IsRequired { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public CalendarEvent Event { get; set; } = null!;
}
