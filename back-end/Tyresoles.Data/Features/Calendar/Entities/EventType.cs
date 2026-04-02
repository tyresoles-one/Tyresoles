namespace Tyresoles.Data.Features.Calendar.Entities;

public class EventType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }

    public ICollection<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
}
