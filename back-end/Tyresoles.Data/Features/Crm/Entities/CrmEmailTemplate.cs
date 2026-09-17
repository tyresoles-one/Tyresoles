using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmEmailTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // Fleet Service, Retread Promo, Invoice Reminder, General
    public string Subject { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
