using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmWhatsappCampaignRecipient
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? ContactId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty; // E.164 format without + (e.g. 919876543210)
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }

    public string Status { get; set; } = "Queued"; // Queued, Sent, Delivered, Read, Replied, Failed, Suppressed
    public string? MetaMessageId { get; set; } // wamid.HBg... returned by Meta Cloud API

    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? RepliedAt { get; set; }

    public string? ReplyMessageText { get; set; }
    public int? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CrmWhatsappCampaign? Campaign { get; set; }
    public CrmContact? Contact { get; set; }
}
