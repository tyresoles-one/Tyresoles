using System;

namespace Tyresoles.Data.Features.Crm.Models;

public class DiscoveredLeadDto
{
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Division { get; set; } = string.Empty;
    public string TargetProduct { get; set; } = string.Empty;
    public string LeadSourceType { get; set; } = "Automated";
    public string LeadSourceChannel { get; set; } = "Google-Maps";
    public string SourceUrl { get; set; } = string.Empty;
    public decimal QualityScore { get; set; } = 0.85m;
    public string ScrapingQuery { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? Snippet { get; set; }
    public string ContactType { get; set; } = "Lead";
    public string? Location { get; set; }
}
