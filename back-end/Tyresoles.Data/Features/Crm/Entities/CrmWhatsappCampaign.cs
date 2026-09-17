using System;
using System.Collections.Generic;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmWhatsappCampaign
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string? LanguageCode { get; set; } = "en";
    public string? SenderPhoneNumberId { get; set; }
    public string? DisplayPhoneNumber { get; set; }
    
    public string Status { get; set; } = "Draft"; // Draft, Scheduled, InProgress, Paused, Completed, Cancelled, Failed
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? TargetSegmentFilterJson { get; set; }
    public string? VariableMappingsJson { get; set; }
    public string? HeaderMediaUrl { get; set; }

    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int RepliedCount { get; set; }
    public int FailedCount { get; set; }

    public decimal CostPerMessage { get; set; } = 0.80m;
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }

    public string? FailureReason { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public CrmWhatsappTemplate? Template { get; set; }
    public ICollection<CrmWhatsappCampaignRecipient> Recipients { get; set; } = new List<CrmWhatsappCampaignRecipient>();
}
