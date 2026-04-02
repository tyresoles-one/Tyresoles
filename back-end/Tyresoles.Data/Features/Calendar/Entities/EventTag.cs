namespace Tyresoles.Data.Features.Calendar.Entities;

public enum EventTagType
{
    User = 0,
    Customer = 1,
    Vendor = 2,
    Topic = 3
}

public class EventTag
{
    public Guid EventId { get; set; }
    public EventTagType TagType { get; set; }
    public string TagKey { get; set; } = string.Empty;

    public CalendarEvent Event { get; set; } = null!;
}
