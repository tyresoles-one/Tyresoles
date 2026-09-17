using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmWhatsappTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = "English"; // e.g. "English", "Hindi", "Marathi"
    public string? LanguageCode { get; set; } = "en"; // e.g. "en", "hi", "mr"
    public string MessageText { get; set; } = string.Empty; // Legacy & raw text representation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Meta Cloud API Fields
    public string? MetaTemplateId { get; set; } // Template ID on Meta's WhatsApp Business Account
    public string Category { get; set; } = "MARKETING"; // MARKETING, UTILITY, AUTHENTICATION
    public string Status { get; set; } = "APPROVED"; // APPROVED, PENDING, REJECTED, PAUSED, DISABLED
    
    public string HeaderType { get; set; } = "NONE"; // NONE, TEXT, IMAGE, VIDEO, DOCUMENT
    public string? HeaderText { get; set; }
    public string? HeaderMediaUrl { get; set; }
    
    public string? BodyText { get; set; } // Body text with {{1}}, {{2}} placeholders
    public string? FooterText { get; set; } // Footer text (e.g. "Reply STOP to opt out")

    public string? ButtonsJson { get; set; } // JSON array of buttons (Quick Reply, Call-to-Action URL/Phone)
    public string? ComponentsJson { get; set; } // Full raw Meta Graph API components structure
    public string? SampleValuesJson { get; set; } // Sample values for variables required for Meta approval
    public string? VariableMappingsJson { get; set; } // Default variable field mappings (e.g. {{1}} -> FullName)

    public string? QualityScore { get; set; } // GREEN, YELLOW, RED, UNKNOWN
    public string? RejectedReason { get; set; }
    public DateTime? SyncedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
