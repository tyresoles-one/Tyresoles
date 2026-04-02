using System.Collections.Generic;
using System.Linq;
using Dataverse.NavLive;
using Microsoft.EntityFrameworkCore;
using Tyresoles.Data;
using Tyresoles.Data.Features.Calendar.Dto;
using Tyresoles.Data.Features.Calendar.Entities;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Calendar;

public sealed class CalendarService : ICalendarService
{
    private readonly CalendarDbContext _db;
    private readonly IDataverseDataService _dataService;

    public CalendarService(CalendarDbContext db, IDataverseDataService dataService)
    {
        _db = db;
        _dataService = dataService;
    }

    public async Task<IReadOnlyList<CalendarEventDto>> GetMyEventsAsync(
        string ownerUserId,
        DateTime fromUtc,
        DateTime toUtc,
        EventTagType? tagTypeFilter = null,
        string? tagKeyFilter = null,
        bool includeRecurrence = true,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var sharedWithMe = await _db.CalendarShares
            .Where(s => s.SharedWithUserId == ownerUserId)
            .Select(s => s.OwnerUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var query = _db.CalendarEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted && (e.OwnerUserId == ownerUserId || sharedWithMe.Contains(e.OwnerUserId)))
            .Where(e => e.ParentEventId == null)
            .Where(e => e.EndUtc >= fromUtc && e.StartUtc <= toUtc);

        if (tagTypeFilter.HasValue && !string.IsNullOrEmpty(tagKeyFilter))
        {
            query = query.Where(e => e.Tags.Any(t => t.TagType == tagTypeFilter && t.TagKey == tagKeyFilter));
        }
        else if (tagTypeFilter.HasValue)
        {
            query = query.Where(e => e.Tags.Any(t => t.TagType == tagTypeFilter));
        }

        var events = await query
            .Include(e => e.Tags)
            .Include(e => e.Reminders)
            .Include(e => e.EventType)
            .Include(e => e.RecurrenceRule)
            .Include(e => e.Attendees)
            .Include(e => e.ExceptionEvents).ThenInclude(x => x.Tags)
            .Include(e => e.ExceptionEvents).ThenInclude(x => x.Reminders)
            .Include(e => e.ExceptionEvents).ThenInclude(x => x.EventType)
            .Include(e => e.ExceptionEvents).ThenInclude(x => x.Attendees)
            .Include(e => e.Tasks)
            .Include(e => e.ExceptionEvents).ThenInclude(x => x.Tasks)
            .OrderBy(e => e.StartUtc)
            .ToListAsync(cancellationToken);

        var result = new List<CalendarEventDto>();

        foreach (var ev in events)
        {
            if (ev.RecurrenceRuleId.HasValue && ev.RecurrenceRule != null && includeRecurrence)
            {
                var duration = ev.EndUtc - ev.StartUtc;
                var excludedStarts = ev.ExceptionEvents.Where(x => x.IsDeleted).Select(x => x.ExceptionOccurrenceStartUtc).Where(x => x.HasValue).Select(x => NormalizeOccurrenceStart(x!.Value)).ToHashSet();
                var overrideByStart = ev.ExceptionEvents.Where(x => !x.IsDeleted && x.ExceptionOccurrenceStartUtc.HasValue).ToDictionary(x => NormalizeOccurrenceStart(x.ExceptionOccurrenceStartUtc!.Value));

                var occurrences = RecurrenceExpander.Expand(ev.StartUtc, ev.EndUtc, ev.RecurrenceRule, fromUtc, toUtc);
                foreach (var (start, end) in occurrences)
                {
                    var occurrenceStart = NormalizeOccurrenceStart(start);
                    if (excludedStarts.Contains(occurrenceStart)) continue;
                    if (overrideByStart.TryGetValue(occurrenceStart, out var exEv))
                    {
                        result.Add(MapToDto(exEv, exEv.StartUtc, exEv.EndUtc));
                        continue;
                    }
                    result.Add(MapToDto(ev, start, end));
                }
            }
            else
            {
                result.Add(MapToDto(ev, ev.StartUtc, ev.EndUtc));
            }
        }

        await ResolveTagDisplayNamesAsync(result, cancellationToken);
        var ordered = result.OrderBy(e => e.StartUtc).ToList();
        if (skip.HasValue || take.HasValue)
        {
            var skipped = skip ?? 0;
            var taken = take ?? ordered.Count;
            return ordered.Skip(skipped).Take(taken).ToList();
        }
        return ordered;
    }

    public async Task<CalendarEventDto?> GetEventByIdAsync(Guid eventId, string currentUserId, CancellationToken cancellationToken = default)
    {
        var ev = await _db.CalendarEvents
            .AsNoTracking()
            .Include(e => e.Tags)
            .Include(e => e.Reminders)
            .Include(e => e.EventType)
            .Include(e => e.RecurrenceRule)
            .Include(e => e.Attendees)
            .Include(e => e.Tasks)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted && (e.OwnerUserId == currentUserId || e.Attendees.Any(a => a.UserId == currentUserId)), cancellationToken);
        if (ev == null) return null;

        var dto = MapToDto(ev, ev.StartUtc, ev.EndUtc);
        dto.Recurrence = ev.RecurrenceRule == null ? null : new RecurrenceDto
        {
            Frequency = ev.RecurrenceRule.Frequency,
            Interval = ev.RecurrenceRule.Interval,
            DaysOfWeek = ev.RecurrenceRule.DaysOfWeek,
            DayOfMonth = ev.RecurrenceRule.DayOfMonth,
            MonthOfYear = ev.RecurrenceRule.MonthOfYear,
            EndByDate = ev.RecurrenceRule.EndByDate,
            OccurrenceCount = ev.RecurrenceRule.OccurrenceCount,
            RRule = ev.RecurrenceRule.RRule
        };
        await ResolveTagDisplayNamesAsync(new List<CalendarEventDto> { dto }, cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<EventTypeDto>> GetEventTypesAsync(CancellationToken cancellationToken = default)
    {
        var list = await _db.EventTypes
            .AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .Select(t => new EventTypeDto { Id = t.Id, Name = t.Name, Color = t.Color, Icon = t.Icon, SortOrder = t.SortOrder })
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<CalendarEventDto?> CreateEventAsync(string ownerUserId, CreateEventInput input, CancellationToken cancellationToken = default)
    {
        Guid? recurrenceRuleId = null;
        if (input.Recurrence != null)
        {
            var rule = new RecurrenceRule
            {
                Id = Guid.NewGuid(),
                Frequency = input.Recurrence.Frequency,
                Interval = input.Recurrence.Interval,
                DaysOfWeek = input.Recurrence.DaysOfWeek,
                DayOfMonth = input.Recurrence.DayOfMonth,
                MonthOfYear = input.Recurrence.MonthOfYear,
                EndByDate = input.Recurrence.EndByDate,
                OccurrenceCount = input.Recurrence.OccurrenceCount,
                RRule = input.Recurrence.RRule
            };
            _db.RecurrenceRules.Add(rule);
            recurrenceRuleId = rule.Id;
        }

        var ev = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            EventTypeId = input.EventTypeId,
            Title = input.Title,
            Description = input.Description,
            StartUtc = input.StartUtc,
            EndUtc = input.EndUtc,
            IsAllDay = input.IsAllDay,
            TimeZoneId = input.TimeZoneId,
            Location = input.Location,
            MeetingLink = input.MeetingLink,
            Visibility = input.Visibility.HasValue ? (EventVisibility)input.Visibility.Value : EventVisibility.Default,
            ShowAs = input.ShowAs.HasValue ? (EventShowAs)input.ShowAs.Value : EventShowAs.Busy,
            Status = input.Status.HasValue ? (EventStatus)input.Status.Value : EventStatus.Confirmed,
            RecurrenceRuleId = recurrenceRuleId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = ownerUserId,
            UpdatedByUserId = ownerUserId
        };

        _db.CalendarEvents.Add(ev);

        if (input.Tags != null)
        {
            foreach (var t in input.Tags)
                _db.EventTags.Add(new EventTag { EventId = ev.Id, TagType = t.TagType, TagKey = t.TagKey });
        }

        var remindersToAdd = input.Reminders?.ToList() ?? new List<ReminderInput>();
        if (remindersToAdd.Count == 0)
        {
            var prefs = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == ownerUserId && p.Channel == ReminderChannel.InApp, cancellationToken);
            if (prefs != null && prefs.DefaultMinutesBefore > 0)
                remindersToAdd.Add(new ReminderInput { RemindAtUtc = input.StartUtc.AddMinutes(-prefs.DefaultMinutesBefore), Channel = ReminderChannel.InApp });
        }
        foreach (var r in remindersToAdd)
            _db.Reminders.Add(new Reminder { Id = Guid.NewGuid(), EventId = ev.Id, RemindAtUtc = r.RemindAtUtc, Channel = r.Channel });

        if (input.Attendees != null)
        {
            foreach (var a in input.Attendees)
                _db.EventAttendees.Add(new EventAttendee { Id = Guid.NewGuid(), EventId = ev.Id, UserId = a.UserId, IsRequired = a.IsRequired, CreatedAt = DateTime.UtcNow });
        }

        if (input.Tasks != null)
        {
            CreateTasks(ev.Id, null, input.Tasks);
        }

        _db.CalendarAuditLogs.Add(new CalendarAuditLog
        {
            EventId = ev.Id,
            Action = CalendarAuditAction.Created,
            UserId = ownerUserId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await GetEventByIdAsync(ev.Id, ownerUserId, cancellationToken);
    }

    public async Task<CalendarEventDto?> UpdateEventAsync(Guid eventId, string currentUserId, UpdateEventInput input, int updateScope = 0, DateTime? occurrenceStartUtc = null, CancellationToken cancellationToken = default)
    {
        var ev = await _db.CalendarEvents
            .Include(e => e.Tags)
            .Include(e => e.Reminders)
            .Include(e => e.RecurrenceRule)
            .Include(e => e.Attendees)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted && e.OwnerUserId == currentUserId, cancellationToken);
        if (ev == null) return null;

        var scope = (UpdateScope)updateScope;
        if (scope == UpdateScope.ThisOccurrence && occurrenceStartUtc.HasValue && ev.RecurrenceRuleId.HasValue)
        {
            var duration = ev.EndUtc - ev.StartUtc;
            var exStart = occurrenceStartUtc.Value;
            var exEnd = exStart + duration;
            var existingEx = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.ParentEventId == eventId && e.ExceptionOccurrenceStartUtc.HasValue && e.ExceptionOccurrenceStartUtc.Value == NormalizeOccurrenceStart(exStart), cancellationToken);
            CalendarEvent target;
            if (existingEx != null)
            {
                target = await _db.CalendarEvents.Include(e => e.Tags).Include(e => e.Reminders).Include(e => e.Attendees).Include(e => e.EventType).FirstAsync(e => e.Id == existingEx.Id, cancellationToken);
            }
            else
            {
                target = new CalendarEvent
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = ev.OwnerUserId,
                    ParentEventId = eventId,
                    ExceptionOccurrenceStartUtc = NormalizeOccurrenceStart(exStart),
                    CalendarId = ev.CalendarId,
                    EventTypeId = ev.EventTypeId,
                    Title = ev.Title,
                    Description = ev.Description,
                    StartUtc = exStart,
                    EndUtc = exEnd,
                    IsAllDay = ev.IsAllDay,
                    TimeZoneId = ev.TimeZoneId,
                    Location = ev.Location,
                    MeetingLink = ev.MeetingLink,
                    Visibility = ev.Visibility,
                    ShowAs = ev.ShowAs,
                    Status = ev.Status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };
                _db.CalendarEvents.Add(target);
                foreach (var t in ev.Tags) _db.EventTags.Add(new EventTag { EventId = target.Id, TagType = t.TagType, TagKey = t.TagKey });
                foreach (var r in ev.Reminders) _db.Reminders.Add(new Reminder { Id = Guid.NewGuid(), EventId = target.Id, RemindAtUtc = r.RemindAtUtc, Channel = r.Channel });
                foreach (var a in ev.Attendees) _db.EventAttendees.Add(new EventAttendee { Id = Guid.NewGuid(), EventId = target.Id, UserId = a.UserId, IsRequired = a.IsRequired, CreatedAt = DateTime.UtcNow });
                CreateTasks(target.Id, null, ev.Tasks.Where(t => t.ParentTaskId == null).Select(t => MapToInput(t)));
            }
            if (input.Title != null) target.Title = input.Title;
            if (input.Description != null) target.Description = input.Description;
            if (input.Location != null) target.Location = input.Location;
            if (input.MeetingLink != null) target.MeetingLink = input.MeetingLink;
            if (input.Visibility.HasValue) target.Visibility = (EventVisibility)input.Visibility.Value;
            if (input.ShowAs.HasValue) target.ShowAs = (EventShowAs)input.ShowAs.Value;
            if (input.Status.HasValue) target.Status = (EventStatus)input.Status.Value;
            if (input.EventTypeId.HasValue) target.EventTypeId = input.EventTypeId.Value == 0 ? null : input.EventTypeId;
            if (input.StartUtc.HasValue) target.StartUtc = input.StartUtc.Value;
            if (input.EndUtc.HasValue) target.EndUtc = input.EndUtc.Value;
            if (input.IsAllDay.HasValue) target.IsAllDay = input.IsAllDay.Value;
            if (input.TimeZoneId != null) target.TimeZoneId = input.TimeZoneId;
            target.UpdatedAt = DateTime.UtcNow;
            target.UpdatedByUserId = currentUserId;
            if (input.Reminders != null) { await _db.Reminders.Where(r => r.EventId == target.Id).ExecuteDeleteAsync(cancellationToken); foreach (var r in input.Reminders) _db.Reminders.Add(new Reminder { Id = Guid.NewGuid(), EventId = target.Id, RemindAtUtc = r.RemindAtUtc, Channel = r.Channel }); }
            if (input.Tags != null) { _db.EventTags.RemoveRange(target.Tags); foreach (var t in input.Tags) _db.EventTags.Add(new EventTag { EventId = target.Id, TagType = t.TagType, TagKey = t.TagKey }); }
            if (input.Attendees != null) { _db.EventAttendees.RemoveRange(target.Attendees); foreach (var a in input.Attendees) _db.EventAttendees.Add(new EventAttendee { Id = Guid.NewGuid(), EventId = target.Id, UserId = a.UserId, IsRequired = a.IsRequired, CreatedAt = DateTime.UtcNow }); }
            if (input.Tasks != null) { await _db.CalendarTasks.Where(t => t.EventId == target.Id).ExecuteDeleteAsync(cancellationToken); CreateTasks(target.Id, null, input.Tasks); }
            _db.CalendarAuditLogs.Add(new CalendarAuditLog { EventId = target.Id, Action = CalendarAuditAction.Updated, UserId = currentUserId, CreatedAtUtc = DateTime.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return await GetEventByIdAsync(target.Id, currentUserId, cancellationToken);
        }

        if (input.Title != null) ev.Title = input.Title;
        if (input.Description != null) ev.Description = input.Description;
        if (input.StartUtc.HasValue) ev.StartUtc = input.StartUtc.Value;
        if (input.EndUtc.HasValue) ev.EndUtc = input.EndUtc.Value;
        if (input.IsAllDay.HasValue) ev.IsAllDay = input.IsAllDay.Value;
        if (input.TimeZoneId != null) ev.TimeZoneId = input.TimeZoneId;
        if (input.Location != null) ev.Location = input.Location;
        if (input.MeetingLink != null) ev.MeetingLink = input.MeetingLink;
        if (input.Visibility.HasValue) ev.Visibility = (EventVisibility)input.Visibility.Value;
        if (input.ShowAs.HasValue) ev.ShowAs = (EventShowAs)input.ShowAs.Value;
        if (input.Status.HasValue) ev.Status = (EventStatus)input.Status.Value;
        if (input.EventTypeId.HasValue) ev.EventTypeId = input.EventTypeId.Value == 0 ? null : input.EventTypeId;

        if (input.Recurrence != null)
        {
            if (ev.RecurrenceRuleId.HasValue && ev.RecurrenceRule != null)
            {
                ev.RecurrenceRule.Frequency = input.Recurrence.Frequency;
                ev.RecurrenceRule.Interval = input.Recurrence.Interval;
                ev.RecurrenceRule.DaysOfWeek = input.Recurrence.DaysOfWeek;
                ev.RecurrenceRule.DayOfMonth = input.Recurrence.DayOfMonth;
                ev.RecurrenceRule.MonthOfYear = input.Recurrence.MonthOfYear;
                ev.RecurrenceRule.EndByDate = input.Recurrence.EndByDate;
                ev.RecurrenceRule.OccurrenceCount = input.Recurrence.OccurrenceCount;
                ev.RecurrenceRule.RRule = input.Recurrence.RRule;
            }
            else
            {
                var rule = new RecurrenceRule
                {
                    Id = Guid.NewGuid(),
                    Frequency = input.Recurrence.Frequency,
                    Interval = input.Recurrence.Interval,
                    DaysOfWeek = input.Recurrence.DaysOfWeek,
                    DayOfMonth = input.Recurrence.DayOfMonth,
                    MonthOfYear = input.Recurrence.MonthOfYear,
                    EndByDate = input.Recurrence.EndByDate,
                    OccurrenceCount = input.Recurrence.OccurrenceCount,
                    RRule = input.Recurrence.RRule
                };
                _db.RecurrenceRules.Add(rule);
                ev.RecurrenceRuleId = rule.Id;
            }
        }

        if (input.Tags != null)
        {
            _db.EventTags.RemoveRange(ev.Tags);
            foreach (var t in input.Tags)
                _db.EventTags.Add(new EventTag { EventId = ev.Id, TagType = t.TagType, TagKey = t.TagKey });
        }

        if (input.Reminders != null)
        {
            _db.Reminders.RemoveRange(ev.Reminders);
            foreach (var r in input.Reminders)
                _db.Reminders.Add(new Reminder { Id = Guid.NewGuid(), EventId = ev.Id, RemindAtUtc = r.RemindAtUtc, Channel = r.Channel });
        }

        if (input.Attendees != null)
        {
            _db.EventAttendees.RemoveRange(ev.Attendees);
            foreach (var a in input.Attendees)
                _db.EventAttendees.Add(new EventAttendee { Id = Guid.NewGuid(), EventId = ev.Id, UserId = a.UserId, IsRequired = a.IsRequired, CreatedAt = DateTime.UtcNow });
        }

        ev.UpdatedAt = DateTime.UtcNow;
        ev.UpdatedByUserId = currentUserId;

        if (input.Tasks != null)
        {
            await _db.CalendarTasks.Where(t => t.EventId == ev.Id).ExecuteDeleteAsync(cancellationToken);
            CreateTasks(ev.Id, null, input.Tasks);
        }

        _db.CalendarAuditLogs.Add(new CalendarAuditLog
        {
            EventId = ev.Id,
            Action = CalendarAuditAction.Updated,
            UserId = currentUserId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await GetEventByIdAsync(ev.Id, currentUserId, cancellationToken);
    }

    public async Task<bool> DeleteEventAsync(Guid eventId, string currentUserId, bool soft = true, int deleteScope = 0, DateTime? occurrenceStartUtc = null, CancellationToken cancellationToken = default)
    {
        var ev = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.OwnerUserId == currentUserId, cancellationToken);
        if (ev == null) return false;

        var scope = (UpdateScope)deleteScope;
        if (scope == UpdateScope.ThisOccurrence && occurrenceStartUtc.HasValue && ev.RecurrenceRuleId.HasValue)
        {
            var exStart = NormalizeOccurrenceStart(occurrenceStartUtc.Value);
            var duration = ev.EndUtc - ev.StartUtc;
            var existingEx = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.ParentEventId == eventId && e.ExceptionOccurrenceStartUtc == exStart, cancellationToken);
            if (existingEx != null)
            {
                existingEx.IsDeleted = true;
                existingEx.UpdatedAt = DateTime.UtcNow;
                existingEx.UpdatedByUserId = currentUserId;
            }
            else
            {
                var exEv = new CalendarEvent
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = ev.OwnerUserId,
                    ParentEventId = eventId,
                    ExceptionOccurrenceStartUtc = exStart,
                    Title = ev.Title,
                    StartUtc = exStart,
                    EndUtc = exStart + duration,
                    IsAllDay = ev.IsAllDay,
                    IsDeleted = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };
                _db.CalendarEvents.Add(exEv);
            }
            _db.CalendarAuditLogs.Add(new CalendarAuditLog { EventId = ev.Id, Action = CalendarAuditAction.Deleted, UserId = currentUserId, CreatedAtUtc = DateTime.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (soft)
        {
            ev.IsDeleted = true;
            ev.UpdatedAt = DateTime.UtcNow;
            ev.UpdatedByUserId = currentUserId;
            _db.CalendarAuditLogs.Add(new CalendarAuditLog { EventId = ev.Id, Action = CalendarAuditAction.Deleted, UserId = currentUserId, CreatedAtUtc = DateTime.UtcNow });
        }
        else
        {
            await _db.Reminders.Where(r => r.EventId == eventId).ExecuteDeleteAsync(cancellationToken);
            await _db.EventTags.Where(t => t.EventId == eventId).ExecuteDeleteAsync(cancellationToken);
            _db.CalendarAuditLogs.Add(new CalendarAuditLog { EventId = ev.Id, Action = CalendarAuditAction.Deleted, UserId = currentUserId, CreatedAtUtc = DateTime.UtcNow });
            _db.CalendarEvents.Remove(ev);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CalendarEventDto>> GetUpcomingRemindersAsync(string userId, DateTime untilUtc, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var reminders = await _db.Reminders
            .AsNoTracking()
            .Where(r => !r.IsSent && r.RemindAtUtc >= now && r.RemindAtUtc <= untilUtc)
            .Where(r => (r.SnoozeUntilUtc == null || r.SnoozeUntilUtc <= now))
            .Include(r => r.Event)
            .Where(r => r.Event != null && !r.Event.IsDeleted && r.Event.OwnerUserId == userId)
            .Select(r => r.Event!)
            .Distinct()
            .Include(e => e.Tags)
            .Include(e => e.Reminders)
            .Include(e => e.EventType)
            .ToListAsync(cancellationToken);

        var dtos = reminders.Select(e => MapToDto(e, e.StartUtc, e.EndUtc)).ToList();
        await ResolveTagDisplayNamesAsync(dtos, cancellationToken);
        return dtos;
    }

    public async Task<bool> SnoozeReminderAsync(Guid reminderId, string userId, DateTime snoozeUntilUtc, CancellationToken cancellationToken = default)
    {
        var reminder = await _db.Reminders
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == reminderId && r.Event != null && r.Event.OwnerUserId == userId, cancellationToken);
        if (reminder == null) return false;
        reminder.SnoozeUntilUtc = snoozeUntilUtc;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CalendarShareDto>> GetSharedCalendarsAsync(string currentUserId, CancellationToken cancellationToken = default)
    {
        var list = await _db.CalendarShares
            .AsNoTracking()
            .Where(s => s.OwnerUserId == currentUserId)
            .OrderBy(s => s.SharedWithUserId)
            .Select(s => new CalendarShareDto { Id = s.Id, OwnerUserId = s.OwnerUserId, SharedWithUserId = s.SharedWithUserId, Permission = (int)s.Permission, CreatedAt = s.CreatedAt })
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<bool> ShareCalendarAsync(string ownerUserId, string sharedWithUserId, int permission, CancellationToken cancellationToken = default)
    {
        var existing = await _db.CalendarShares.FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId && s.SharedWithUserId == sharedWithUserId, cancellationToken);
        if (existing != null)
        {
            existing.Permission = (CalendarSharePermission)permission;
        }
        else
        {
            _db.CalendarShares.Add(new CalendarShare
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                SharedWithUserId = sharedWithUserId,
                Permission = (CalendarSharePermission)permission,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RespondToInviteAsync(Guid eventId, string userId, int response, CancellationToken cancellationToken = default)
    {
        var attendee = await _db.EventAttendees.FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == userId, cancellationToken);
        if (attendee == null) return false;
        attendee.Response = (AttendeeResponse)response;
        attendee.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<NotificationPreferenceDto?> GetNotificationPreferenceAsync(string userId, CancellationToken cancellationToken = default)
    {
        var prefs = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.Channel == ReminderChannel.InApp)
            .FirstOrDefaultAsync(cancellationToken);
        if (prefs == null) return null;
        return new NotificationPreferenceDto { Channel = (int)prefs.Channel, DefaultMinutesBefore = prefs.DefaultMinutesBefore, EmailEnabled = prefs.EmailEnabled, PushEnabled = prefs.PushEnabled };
    }

    public async Task<bool> SetNotificationPreferenceAsync(string userId, NotificationPreferenceDto input, CancellationToken cancellationToken = default)
    {
        var prefs = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId && p.Channel == (ReminderChannel)input.Channel, cancellationToken);
        if (prefs == null)
        {
            _db.NotificationPreferences.Add(new NotificationPreference
            {
                UserId = userId,
                Channel = (ReminderChannel)input.Channel,
                DefaultMinutesBefore = input.DefaultMinutesBefore,
                EmailEnabled = input.EmailEnabled,
                PushEnabled = input.PushEnabled,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            prefs.DefaultMinutesBefore = input.DefaultMinutesBefore;
            prefs.EmailEnabled = input.EmailEnabled;
            prefs.PushEnabled = input.PushEnabled;
            prefs.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<FreeBusyDto>> GetFreeBusyAsync(IReadOnlyList<string> userIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var sharedByUser = await _db.CalendarShares
            .Where(s => userIds.Contains(s.SharedWithUserId))
            .Select(s => new { s.SharedWithUserId, s.OwnerUserId })
            .ToListAsync(cancellationToken);
        var ownerIds = userIds.Union(sharedByUser.Select(x => x.OwnerUserId)).Distinct().ToList();

        var events = await _db.CalendarEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.ParentEventId == null && ownerIds.Contains(e.OwnerUserId))
            .Where(e => e.EndUtc > fromUtc && e.StartUtc < toUtc)
            .Where(e => e.ShowAs != EventShowAs.Free)
            .Select(e => new { e.OwnerUserId, e.Id, e.Title, e.StartUtc, e.EndUtc })
            .ToListAsync(cancellationToken);

        var recurring = await _db.CalendarEvents
            .AsNoTracking()
            .Include(e => e.RecurrenceRule)
            .Include(e => e.ExceptionEvents)
            .Where(e => !e.IsDeleted && e.ParentEventId == null && ownerIds.Contains(e.OwnerUserId) && e.RecurrenceRuleId != null)
            .ToListAsync(cancellationToken);

        var expanded = new List<(string OwnerUserId, Guid Id, string Title, DateTime Start, DateTime End)>();
        foreach (var ev in recurring)
        {
            if (ev.RecurrenceRule == null || ev.ShowAs == EventShowAs.Free) continue;
            var excluded = ev.ExceptionEvents.Where(x => x.IsDeleted).Select(x => x.ExceptionOccurrenceStartUtc).Where(x => x.HasValue).Select(x => NormalizeOccurrenceStart(x!.Value)).ToHashSet();
            var occurrences = RecurrenceExpander.Expand(ev.StartUtc, ev.EndUtc, ev.RecurrenceRule, fromUtc, toUtc);
            foreach (var (start, end) in occurrences)
            {
                if (excluded.Contains(NormalizeOccurrenceStart(start))) continue;
                expanded.Add((ev.OwnerUserId, ev.Id, ev.Title ?? "", start, end));
            }
        }

        var allSlots = events.Select(e => (e.OwnerUserId, e.Id, e.Title ?? "", e.StartUtc, e.EndUtc)).Concat(expanded).ToList();
        return ownerIds.Select(uid => new FreeBusyDto
        {
            UserId = uid,
            Busy = allSlots.Where(s => s.OwnerUserId == uid).Select(s => new FreeBusySlotDto { StartUtc = s.Item4, EndUtc = s.Item5, EventId = s.Id, Title = s.Item3 }).OrderBy(x => x.StartUtc).ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<CalendarAuditLogEntryDto>> GetCalendarAuditLogAsync(Guid? eventId, string? userId, int limit, CancellationToken cancellationToken = default)
    {
        var query = _db.CalendarAuditLogs.AsNoTracking();
        if (eventId.HasValue) query = query.Where(x => x.EventId == eventId.Value);
        if (!string.IsNullOrEmpty(userId)) query = query.Where(x => x.UserId == userId);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Min(limit, 500))
            .Select(x => new CalendarAuditLogEntryDto { Id = x.Id, EventId = x.EventId, Action = (int)x.Action, UserId = x.UserId, Payload = x.Payload, CreatedAtUtc = x.CreatedAtUtc })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> GetConflictsAsync(string userId, DateTime startUtc, DateTime endUtc, Guid? excludeEventId, CancellationToken cancellationToken = default)
    {
        var sharedWithMe = await _db.CalendarShares.Where(s => s.SharedWithUserId == userId).Select(s => s.OwnerUserId).Distinct().ToListAsync(cancellationToken);
        var q = _db.CalendarEvents.AsNoTracking()
            .Where(e => !e.IsDeleted && (e.OwnerUserId == userId || sharedWithMe.Contains(e.OwnerUserId)))
            .Where(e => e.ParentEventId == null)
            .Where(e => e.EndUtc > startUtc && e.StartUtc < endUtc)
            .Where(e => e.ShowAs != EventShowAs.Free);
        if (excludeEventId.HasValue) q = q.Where(e => e.Id != excludeEventId.Value);
        var list = await q.Include(e => e.EventType).Include(e => e.RecurrenceRule).Include(e => e.Tags).Include(e => e.Reminders).Include(e => e.Attendees)
            .OrderBy(e => e.StartUtc).ToListAsync(cancellationToken);
        var result = list.Select(e => MapToDto(e, e.StartUtc, e.EndUtc)).ToList();
        await ResolveTagDisplayNamesAsync(result, cancellationToken);
        return result;
    }

    public async Task<string> ExportIcsAsync(string userId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var events = await GetMyEventsAsync(userId, fromUtc, toUtc, includeRecurrence: true, cancellationToken: cancellationToken);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Tyresoles//Calendar//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        foreach (var e in events)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{e.Id}@tyresoles");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"DTSTART:{(e.IsAllDay ? e.StartUtc.ToString("yyyyMMdd") : e.StartUtc.ToString("yyyyMMddTHHmmssZ"))}");
            sb.AppendLine($"DTEND:{(e.IsAllDay ? e.EndUtc.ToString("yyyyMMdd") : e.EndUtc.ToString("yyyyMMddTHHmmssZ"))}");
            if (e.IsAllDay) sb.AppendLine("TRANSP:TRANSPARENT");
            sb.AppendLine($"SUMMARY:{EscapeIcsText(e.Title)}");
            if (!string.IsNullOrEmpty(e.Description)) sb.AppendLine($"DESCRIPTION:{EscapeIcsText(e.Description)}");
            if (!string.IsNullOrEmpty(e.Location)) sb.AppendLine($"LOCATION:{EscapeIcsText(e.Location)}");
            sb.AppendLine("END:VEVENT");
        }
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscapeIcsText(string s)
    {
        return s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
    }

    private static DateTime NormalizeOccurrenceStart(DateTime dt)
    {
        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, DateTimeKind.Utc);
    }

    private static CalendarEventDto MapToDto(CalendarEvent ev, DateTime startUtc, DateTime endUtc)
    {
        return new CalendarEventDto
        {
            Id = ev.Id,
            OwnerUserId = ev.OwnerUserId,
            EventTypeId = ev.EventTypeId,
            EventTypeName = ev.EventType?.Name,
            EventTypeColor = ev.EventType?.Color,
            Title = ev.Title,
            Description = ev.Description,
            StartUtc = startUtc,
            EndUtc = endUtc,
            IsAllDay = ev.IsAllDay,
            TimeZoneId = ev.TimeZoneId,
            Location = ev.Location,
            MeetingLink = ev.MeetingLink,
            Visibility = (int)ev.Visibility,
            ShowAs = (int)ev.ShowAs,
            Status = (int)ev.Status,
            RecurrenceRuleId = ev.RecurrenceRuleId,
            ParentEventId = ev.ParentEventId,
            ExceptionOccurrenceStartUtc = ev.ExceptionOccurrenceStartUtc,
            Tags = ev.Tags.Select(t => new EventTagDto { TagType = t.TagType, TagKey = t.TagKey }).ToList(),
            Reminders = ev.Reminders.Select(r => new ReminderDto { Id = r.Id, RemindAtUtc = r.RemindAtUtc, Channel = r.Channel, IsSent = r.IsSent, SnoozeUntilUtc = r.SnoozeUntilUtc }).ToList(),
            Attendees = ev.Attendees.Select(a => new EventAttendeeDto { Id = a.Id, UserId = a.UserId, Email = a.Email, Response = (int)a.Response, IsRequired = a.IsRequired, RespondedAt = a.RespondedAt }).ToList(),
            Tasks = MapTasks(ev.Tasks.Where(t => t.ParentTaskId == null).ToList())
        };
    }

    private static List<CalendarTaskDto> MapTasks(IEnumerable<CalendarTask> tasks)
    {
        return tasks.OrderBy(t => t.SortOrder).Select(t => new CalendarTaskDto
        {
            Id = t.Id,
            ParentTaskId = t.ParentTaskId,
            Title = t.Title,
            IsCompleted = t.IsCompleted,
            SortOrder = t.SortOrder,
            SubTasks = MapTasks(t.SubTasks)
        }).ToList();
    }

    public async Task<bool> ToggleTaskStatusAsync(Guid taskId, bool isCompleted, string userId, CancellationToken cancellationToken = default)
    {
        var task = await _db.CalendarTasks
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
            
        if (task == null || task.Event.OwnerUserId != userId) return false;
        
        task.IsCompleted = isCompleted;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ResolveTagDisplayNamesAsync(IList<CalendarEventDto> dtos, CancellationToken cancellationToken)
    {
        var userKeys = dtos.SelectMany(d => d.Tags.Where(t => t.TagType == EventTagType.User).Select(t => t.TagKey)).Distinct().ToList();
        var customerKeys = dtos.SelectMany(d => d.Tags.Where(t => t.TagType == EventTagType.Customer).Select(t => t.TagKey)).Distinct().ToList();
        var vendorKeys = dtos.SelectMany(d => d.Tags.Where(t => t.TagType == EventTagType.Vendor).Select(t => t.TagKey)).Distinct().ToList();

        var userNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var customerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var vendorNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var navScope = _dataService.ForNavLive();
        if (userKeys.Count > 0)
        {
            var users = await navScope.Query<User>().Where(x => userKeys.Contains(x.UserName)).ToArrayAsync(cancellationToken);
            foreach (var u in users)
                if (u?.UserName != null) userNames[u.UserName] = u.FullName ?? u.UserName;
        }
        if (customerKeys.Count > 0)
        {
            var customers = await navScope.Query<Customer>().Where(x => customerKeys.Contains(x.No)).ToArrayAsync(cancellationToken);
            foreach (var c in customers)
                if (c?.No != null) customerNames[c.No] = c.Name ?? c.No;
        }
        if (vendorKeys.Count > 0)
        {
            var vendors = await navScope.Query<Vendor>().Where(x => vendorKeys.Contains(x.No)).ToArrayAsync(cancellationToken);
            foreach (var v in vendors)
                if (v?.No != null) vendorNames[v.No] = v.Name ?? v.No;
        }

        foreach (var dto in dtos)
        {
            foreach (var tag in dto.Tags)
            {
                tag.DisplayName = tag.TagType switch
                {
                    EventTagType.User => userNames.TryGetValue(tag.TagKey, out var un) ? un : tag.TagKey,
                    EventTagType.Customer => customerNames.TryGetValue(tag.TagKey, out var cn) ? cn : tag.TagKey,
                    EventTagType.Vendor => vendorNames.TryGetValue(tag.TagKey, out var vn) ? vn : tag.TagKey,
                    _ => tag.TagKey
                };
            }
        }
    }

    private void CreateTasks(Guid eventId, Guid? parentTaskId, IEnumerable<CalendarTaskInput> tasks)
    {
        foreach (var t in tasks)
        {
            var taskId = Guid.NewGuid();
            var task = new CalendarTask
            {
                Id = taskId,
                EventId = eventId,
                ParentTaskId = parentTaskId,
                Title = t.Title,
                IsCompleted = t.IsCompleted,
                SortOrder = t.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.CalendarTasks.Add(task);
            if (t.SubTasks != null && t.SubTasks.Count > 0)
            {
                CreateTasks(eventId, taskId, t.SubTasks);
            }
        }
    }

    private static CalendarTaskInput MapToInput(CalendarTask t)
    {
        return new CalendarTaskInput
        {
            Title = t.Title,
            IsCompleted = t.IsCompleted,
            SortOrder = t.SortOrder,
            SubTasks = t.SubTasks.Select(st => MapToInput(st)).ToList()
        };
    }
}
