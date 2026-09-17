using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmWhatsappInboundMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FromPhoneNumber { get; set; } = string.Empty;
    public string? ProfileName { get; set; }
    public string MetaMessageId { get; set; } = string.Empty;
    public string? ContextWamid { get; set; } // Points to original outbound message wamid if reply
    public Guid? CampaignId { get; set; }
    public Guid? ContactId { get; set; }
    public string MessageType { get; set; } = "text"; // text, button, interactive, image, etc.
    public string? MessageBody { get; set; }
    public string? ButtonPayload { get; set; }
    public bool IsProcessed { get; set; }
    public string? FollowupStatus { get; set; } = "Pending"; // Pending, Contacted, Converted, Dismissed
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
