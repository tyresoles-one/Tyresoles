using Tyresoles.Sql.Abstractions;

namespace Dataverse.NavLive;

public partial class ResponsibilityCenter
{
    [NavColumn("Target Multiplier")]
    public decimal TargetMultiplier { get; set; }
}
