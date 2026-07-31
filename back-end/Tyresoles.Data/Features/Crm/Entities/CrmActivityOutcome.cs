namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmActivityOutcome
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ActivityTypeId { get; set; }
    public bool IsPositive { get; set; }
}
