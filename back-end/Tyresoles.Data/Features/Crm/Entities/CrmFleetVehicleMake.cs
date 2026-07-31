namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmFleetVehicleMake
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
}
