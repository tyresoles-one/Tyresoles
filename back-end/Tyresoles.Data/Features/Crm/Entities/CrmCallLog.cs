using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmCallLog
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public CrmContact? Contact { get; set; }
    public DateTime CallDate { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
