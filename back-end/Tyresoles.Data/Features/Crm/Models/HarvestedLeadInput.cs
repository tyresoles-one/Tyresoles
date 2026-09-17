using System;
using System.Collections.Generic;

namespace Tyresoles.Data.Features.Crm.Models;

public class HarvestedLeadInput
{
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string MobileNo { get; set; } = string.Empty;
    public string? MobileNo2 { get; set; }
    public string? EmailIds { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? RespCenter { get; set; }
    public string? Division { get; set; } = "Tyresoles"; // "Tyresoles" or "Ecoflex"
    public string? TargetProduct { get; set; }            // "Commercial Retreading", "Tuffloor", etc.
    public string LeadSourceType { get; set; } = "Automated"; // "Automated", "Manual", "Tender"
    public string? LeadSourceChannel { get; set; }       // "Google-Maps", "GeM-Tender", "IndiaMART"
    public string? SourceUrl { get; set; }
    public decimal? QualityScore { get; set; }
    public string? ScrapingQuery { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public string? Website { get; set; }
    public string? Snippet { get; set; }
    public string? ContactType { get; set; } = "Lead";
    public string? Location { get; set; }
}

public class LeadImportBatchResult
{
    public int TotalSubmitted { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Messages { get; set; } = new();
}
