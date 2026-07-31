using System;

namespace Tyresoles.Data.Features.Crm.Models;

public class CrmProductInput
{
    public Guid? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? ProductGroup { get; set; }
    public decimal FinalPrice { get; set; }
    public string? RespCenters { get; set; }
    public string? WhatsappImageCode { get; set; }
}
