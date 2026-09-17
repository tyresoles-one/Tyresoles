using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmEmailCampaign
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
    public string FromName { get; set; } = "Tyresoles Fleet Team";
    public string FromEmail { get; set; } = "updates@tyresoles.in";
    public string? ReplyToEmail { get; set; }
    public string CampaignType { get; set; } = "Broadcast"; // Broadcast, Triggered
    public string ContentType { get; set; } = "Html"; // Html, PlainText
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
    
    public string Status { get; set; } = "Draft"; // Draft, Scheduled, InProgress, Paused, Completed, Cancelled
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? TargetSegmentFilterJson { get; set; }
    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int OpenedCount { get; set; }
    public int UniqueOpenedCount { get; set; }
    public int ClickedCount { get; set; }
    public int UniqueClickedCount { get; set; }
    public int BouncedCount { get; set; }
    public int UnsubscribedCount { get; set; }
    public int SpamComplaintCount { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
