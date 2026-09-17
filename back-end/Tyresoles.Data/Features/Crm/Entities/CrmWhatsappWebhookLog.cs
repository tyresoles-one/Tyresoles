using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmWhatsappWebhookLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = "unknown"; // statuses, messages, handshake, ping, simulated, unknown
    public string? FromPhoneNumber { get; set; }
    public string? MetaMessageId { get; set; }
    public string ProcessingStatus { get; set; } = "Received"; // Processed, SignatureMismatch, RecipientNotFound, Error, Ignored, Simulated
    public string RawPayload { get; set; } = string.Empty;
    public string? SignatureHeader { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? RecipientId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
