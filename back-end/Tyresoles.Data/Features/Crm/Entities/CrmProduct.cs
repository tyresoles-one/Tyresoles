using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmProduct
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? ProductGroup { get; set; }
    public decimal FinalPrice { get; set; }
    public string? RespCenters { get; set; }
    public string? WhatsappImageCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
