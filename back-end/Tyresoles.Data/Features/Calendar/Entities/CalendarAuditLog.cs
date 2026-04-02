namespace Tyresoles.Data.Features.Calendar.Entities;

public enum CalendarAuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    Shared = 3
}

public class CalendarAuditLog
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public CalendarAuditAction Action { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
