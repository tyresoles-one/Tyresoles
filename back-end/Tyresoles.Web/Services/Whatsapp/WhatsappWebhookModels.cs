using System;

namespace Tyresoles.Web.Services.Whatsapp;

public class SimulateWhatsappWebhookRequest
{
    public string EventType { get; set; } = "messages"; // "messages", "delivered", "read", "failed", "button", "stop"
    public string? FromPhoneNumber { get; set; } = "919876543210";
    public string? CustomerName { get; set; } = "Test Fleet Customer";
    public string? MessageText { get; set; } = "Hello, I am interested in truck tyre retreading quote.";
    public string? ButtonPayload { get; set; }
    public string? MetaMessageId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? RecipientId { get; set; }
}

public class SimulateWhatsappWebhookResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? LogId { get; set; }
    public Guid? InboundMessageId { get; set; }
    public string? MetaMessageId { get; set; }
}
