namespace Tyresoles.Data.Features.Crm.Models;

public enum CrmMasterType
{
    ContactType,
    ContactCategory,
    Source,
    SourceChannel,
    Stage,
    Priority,
    ActivityType,
    ActivityOutcome,
    EntityType,
    VehicleType,
    VehicleMake,
    VehicleModel,
    Application,
    Language
}

public class CrmMasterItem
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public bool IsPositive { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
