using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmEmailSuppressionList
{
    public Guid Id { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string Reason { get; set; } = "Unsubscribe"; // HardBounce, SpamComplaint, Unsubscribe, Manual
    public string? DiagnosticCode { get; set; }
    public Guid? SourceCampaignId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
