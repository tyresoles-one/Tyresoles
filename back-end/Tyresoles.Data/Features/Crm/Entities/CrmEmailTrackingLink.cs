using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmEmailTrackingLink
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string LinkHash { get; set; } = string.Empty;
    public int ClickCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CrmEmailCampaign? Campaign { get; set; }
}
