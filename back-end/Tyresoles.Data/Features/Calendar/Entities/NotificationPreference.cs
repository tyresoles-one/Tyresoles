namespace Tyresoles.Data.Features.Calendar.Entities;

public class NotificationPreference
{
    public string UserId { get; set; } = string.Empty;
    public ReminderChannel Channel { get; set; }
    public int DefaultMinutesBefore { get; set; } = 15;
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}
