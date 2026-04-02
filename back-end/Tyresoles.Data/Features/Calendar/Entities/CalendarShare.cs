namespace Tyresoles.Data.Features.Calendar.Entities;

public enum CalendarSharePermission
{
    View = 0,
    Edit = 1,
    Manage = 2
}

public class CalendarShare
{
    public Guid Id { get; set; }
    public Guid? CalendarId { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string SharedWithUserId { get; set; } = string.Empty;
    public CalendarSharePermission Permission { get; set; }
    public DateTime CreatedAt { get; set; }
}
