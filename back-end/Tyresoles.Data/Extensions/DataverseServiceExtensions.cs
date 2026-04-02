using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data;

public static class DataverseServiceExtensions
{
    /// <summary>Returns a tenant scope for the NavLive database.</summary>
    public static ITenantScope ForNavLive(this IDataverseDataService dataService) => dataService.ForTenant("NavLive");
}
