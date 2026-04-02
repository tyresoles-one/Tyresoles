using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar.Dto;

public class ReminderInput
{
    public DateTime RemindAtUtc { get; set; }
    public ReminderChannel Channel { get; set; } = ReminderChannel.InApp;
}
