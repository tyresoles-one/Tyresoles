using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmAgentContact
{
    public Guid Id { get; set; }
    public string AgentUsername { get; set; } = string.Empty;
    public Guid ContactId { get; set; }
    public CrmContact? Contact { get; set; }
    public DateTime AllocatedAt { get; set; }
    public DateTime? DeallocatedAt { get; set; }
    public string? DeallocatedBy { get; set; }
    public string? LastCallOutcome { get; set; }
    public DateTime? LastCallDate { get; set; }
    public string? LastCallNotes { get; set; }
    public int CallCount { get; set; }
}
