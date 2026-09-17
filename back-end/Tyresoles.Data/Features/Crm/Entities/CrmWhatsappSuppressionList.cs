using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmWhatsappSuppressionList
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty; // E.164 format (e.g. 919876543210)
    public string Reason { get; set; } = "UserReplyStop"; // UserReplyStop, Unsubscribed, InvalidNumber, RepeatedFailure, Manual
    public Guid? ContactId { get; set; }
    public string Source { get; set; } = "Webhook"; // Webhook, Agent, Manual
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
