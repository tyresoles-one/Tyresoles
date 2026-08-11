using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmCallReminder
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public CrmContact? Contact { get; set; }
    public DateTime ReminderDate { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
