using System;

namespace Tyresoles.Data.Features.Crm.Models;

public class CrmContactFleetDetailInput
{
    public Guid? Id { get; set; }
    public Guid ContactId { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int Quantity { get; set; }
    public string? TyreSize { get; set; }
    public string? Application { get; set; }
}
