using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmEmailCampaignRecipient
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? ContactId { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }

    public string Status { get; set; } = "Queued"; // Queued, Sent, Delivered, Opened, Clicked, Bounced, Suppressed, Failed
    public string TrackingToken { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public int OpenCount { get; set; }
    public DateTime? ClickedAt { get; set; }
    public int ClickCount { get; set; }
    public DateTime? BouncedAt { get; set; }
    public string? BounceType { get; set; } // Permanent, Transient, Undetermined
    public string? BounceReason { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CrmEmailCampaign? Campaign { get; set; }
    public CrmContact? Contact { get; set; }
}
