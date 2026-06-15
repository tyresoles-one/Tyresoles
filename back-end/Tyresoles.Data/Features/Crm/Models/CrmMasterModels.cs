namespace Tyresoles.Data.Features.Crm.Models;

public enum CrmMasterType
{
    ContactType,
    Source,
    Stage,
    Priority,
    ActivityType,
    ActivityOutcome
}

public class CrmMasterItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
