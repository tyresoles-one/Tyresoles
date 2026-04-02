using System;
using System.Collections.Generic;

namespace Tyresoles.Data.Features.Calendar.Entities;

public class CalendarTask
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CalendarEvent Event { get; set; } = null!;
    public CalendarTask? ParentTask { get; set; }
    public ICollection<CalendarTask> SubTasks { get; set; } = new List<CalendarTask>();
}
