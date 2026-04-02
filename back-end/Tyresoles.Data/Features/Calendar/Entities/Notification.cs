namespace Tyresoles.Data.Features.Calendar.Entities;

public enum NotificationType
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Success = 3
}

public class Notification
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.Info;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
