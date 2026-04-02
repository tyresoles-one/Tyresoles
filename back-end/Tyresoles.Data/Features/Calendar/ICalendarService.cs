using Tyresoles.Data.Features.Calendar.Dto;
using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar;

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEventDto>> GetMyEventsAsync(
        string ownerUserId,
        DateTime fromUtc,
        DateTime toUtc,
        EventTagType? tagTypeFilter = null,
        string? tagKeyFilter = null,
        bool includeRecurrence = true,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default);

    Task<CalendarEventDto?> GetEventByIdAsync(Guid eventId, string currentUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventTypeDto>> GetEventTypesAsync(CancellationToken cancellationToken = default);

    Task<CalendarEventDto?> CreateEventAsync(string ownerUserId, CreateEventInput input, CancellationToken cancellationToken = default);

    Task<CalendarEventDto?> UpdateEventAsync(Guid eventId, string currentUserId, UpdateEventInput input, int updateScope = 0, DateTime? occurrenceStartUtc = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteEventAsync(Guid eventId, string currentUserId, bool soft = true, int deleteScope = 0, DateTime? occurrenceStartUtc = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEventDto>> GetUpcomingRemindersAsync(string userId, DateTime untilUtc, CancellationToken cancellationToken = default);

    Task<bool> SnoozeReminderAsync(Guid reminderId, string userId, DateTime snoozeUntilUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarShareDto>> GetSharedCalendarsAsync(string currentUserId, CancellationToken cancellationToken = default);

    Task<bool> ShareCalendarAsync(string ownerUserId, string sharedWithUserId, int permission, CancellationToken cancellationToken = default);

    Task<bool> RespondToInviteAsync(Guid eventId, string userId, int response, CancellationToken cancellationToken = default);

    Task<NotificationPreferenceDto?> GetNotificationPreferenceAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> SetNotificationPreferenceAsync(string userId, NotificationPreferenceDto input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FreeBusyDto>> GetFreeBusyAsync(IReadOnlyList<string> userIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarAuditLogEntryDto>> GetCalendarAuditLogAsync(Guid? eventId, string? userId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEventDto>> GetConflictsAsync(string userId, DateTime startUtc, DateTime endUtc, Guid? excludeEventId, CancellationToken cancellationToken = default);

    Task<string> ExportIcsAsync(string userId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<bool> ToggleTaskStatusAsync(Guid taskId, bool isCompleted, string userId, CancellationToken cancellationToken = default);
}

public class NotificationPreferenceDto
{
    public int Channel { get; set; }
    public int DefaultMinutesBefore { get; set; }
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
}

public class EventTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}
