using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmContact
{
    public Guid Id { get; set; }
    public string? ContactType { get; set; }
    public string? ContactCategory { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? MobileNo { get; set; }
    public string? MobileNo2 { get; set; }
    public string? EmailIds { get; set; }
    public bool IsDecisionMaker { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? RespCenter { get; set; }
    public string? ERPCustomerNos { get; set; }
    public string? ERPAreaCodes { get; set; }
    public string? Products { get; set; }
    public string? Tags { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime? LastCallDate { get; set; }
    public string? LastCallOutcome { get; set; }

    public string LeadSourceType { get; set; } = "Manual";
    public string? LeadSourceChannel { get; set; }
    public string? SourceUrl { get; set; }
    public string? Division { get; set; }
    public string? TargetProduct { get; set; }
    public decimal? QualityScore { get; set; }
    public string? ScrapingQuery { get; set; }
    public DateTime? HarvestedAt { get; set; }
    public string? Website { get; set; }
    public string? Snippet { get; set; }
    public string? PrefLanguage { get; set; }
    public string? Location { get; set; }
}
