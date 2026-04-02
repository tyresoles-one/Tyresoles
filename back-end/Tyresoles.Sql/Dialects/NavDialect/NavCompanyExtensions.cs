using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Dialects.NavDialect;

public static class NavCompanyExtensions
{
    /// <summary>
    /// Reroutes table compilation strictly to a specified NAV Company syntax (e.g., [CRONUS$Customer]).
    /// </summary>
    public static ITenantScope NavCompany(this IDataverse dataverse, string companyName, string tenant = "NavLive")
    {
        return dataverse.ForTenant(tenant).WithCompany(companyName);
    }
}
