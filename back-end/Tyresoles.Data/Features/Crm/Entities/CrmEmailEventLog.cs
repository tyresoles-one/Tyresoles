using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmEmailEventLog
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? RecipientId { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // Send, Delivery, Open, Click, Bounce, Complaint, Unsubscribe
    public string? Details { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public bool IsMachineOpen { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public CrmEmailCampaign? Campaign { get; set; }
}
