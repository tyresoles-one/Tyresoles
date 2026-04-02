using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar.Dto;

public class RecurrenceInput
{
    public RecurrenceFrequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    public string? DaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public int? MonthOfYear { get; set; }
    public DateOnly? EndByDate { get; set; }
    public int? OccurrenceCount { get; set; }
    public string? RRule { get; set; }
}
