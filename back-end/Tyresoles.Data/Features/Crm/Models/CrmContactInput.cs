using System;

namespace Tyresoles.Data.Features.Crm.Models;

public class CrmContactInput
{
    public Guid? Id { get; set; }
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
}
