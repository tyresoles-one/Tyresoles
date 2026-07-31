namespace Tyresoles.Data.Features.Crm.Models;

public enum CrmMasterType
{
    ContactType,
    ContactCategory,
    Source,
    Stage,
    Priority,
    ActivityType,
    ActivityOutcome,
    EntityType,
    VehicleType,
    VehicleMake,
    VehicleModel,
    Application
}

public class CrmMasterItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public bool IsPositive { get; set; }
}
