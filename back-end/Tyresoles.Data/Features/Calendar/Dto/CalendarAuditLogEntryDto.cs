namespace Tyresoles.Data.Features.Calendar.Dto;

public class CalendarAuditLogEntryDto
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public int Action { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
