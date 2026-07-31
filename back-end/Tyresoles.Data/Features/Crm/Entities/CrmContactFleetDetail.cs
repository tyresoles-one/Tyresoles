using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmContactFleetDetail
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int Quantity { get; set; }
    public string? TyreSize { get; set; }
    public string? Application { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
