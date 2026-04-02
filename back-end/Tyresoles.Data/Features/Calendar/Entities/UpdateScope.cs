namespace Tyresoles.Data.Features.Calendar.Entities;

/// <summary>When updating or deleting a recurring event.</summary>
public enum UpdateScope
{
    All = 0,
    ThisOccurrence = 1,
    ThisAndFuture = 2
}
