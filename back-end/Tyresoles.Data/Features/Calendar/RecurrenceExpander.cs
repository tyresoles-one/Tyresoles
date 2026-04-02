using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar;

public static class RecurrenceExpander
{
    /// <summary>
    /// Expands a recurring event into individual occurrences within the given range.
    /// Returns (StartUtc, EndUtc) for each occurrence. The base event's start/end are used as the template.
    /// </summary>
    public static IEnumerable<(DateTime StartUtc, DateTime EndUtc)> Expand(
        DateTime baseStartUtc,
        DateTime baseEndUtc,
        RecurrenceRule rule,
        DateTime rangeFromUtc,
        DateTime rangeToUtc)
    {
        var duration = baseEndUtc - baseStartUtc;
        var current = baseStartUtc;
        var count = 0;
        var maxOccurrences = rule.OccurrenceCount ?? 9999;

        while (current < rangeToUtc && count < maxOccurrences)
        {
            if (rule.EndByDate.HasValue && current.Date > rule.EndByDate.Value.ToDateTime(TimeOnly.MinValue))
                yield break;

            var occurrenceEnd = current + duration;
            if (current <= rangeToUtc && occurrenceEnd >= rangeFromUtc)
            {
                yield return (current, occurrenceEnd);
                count++;
            }

            current = rule.Frequency switch
            {
                RecurrenceFrequency.Daily => current.AddDays(rule.Interval),
                RecurrenceFrequency.Weekly => AddWeekly(current, rule),
                RecurrenceFrequency.Monthly => current.AddMonths(rule.Interval),
                RecurrenceFrequency.Yearly => current.AddYears(rule.Interval),
                _ => current.AddDays(rule.Interval)
            };
        }
    }

    private static DateTime AddWeekly(DateTime current, RecurrenceRule rule)
    {
        var next = current.AddDays(7 * rule.Interval);
        if (string.IsNullOrEmpty(rule.DaysOfWeek))
            return next;
        // DaysOfWeek: e.g. "1,3,5" for Mon, Wed, Fri - 0=Sun, 1=Mon, ... 6=Sat
        try
        {
            var days = (rule.DaysOfWeek ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var d) ? d : -1)
                .Where(d => d >= 0 && d <= 6)
                .ToHashSet();
            if (days.Count == 0) return next;
            // Find next occurrence that falls on one of the specified days
            for (var i = 0; i < 8; i++)
            {
                var d = next.AddDays(i);
                if (days.Contains((int)d.DayOfWeek))
                    return d;
            }
        }
        catch { /* ignore */ }
        return next;
    }
}
