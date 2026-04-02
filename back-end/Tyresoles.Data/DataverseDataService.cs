using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data;

/// <summary>
/// Entry point for data access and business logic. Uses Tyresoles.Sql (IDataverse) and generated models.
/// Generated models live in namespace Dataverse.{SchemaSource} (e.g. Dataverse.NavLive).
/// </summary>
public interface IDataverseDataService
{
    IDataverse Dataverse { get; }
    ITenantScope ForTenant(string tenantKey);
    /// <summary>Returns a tenant scope for the NavLive (Business Central) database.</summary>
    ITenantScope ForNavLive();
}

public sealed class DataverseDataService : IDataverseDataService
{
    public DataverseDataService(IDataverse dataverse)
    {
        Dataverse = dataverse;
    }

    public IDataverse Dataverse { get; }

    public ITenantScope ForTenant(string tenantKey) => Dataverse.ForTenant(tenantKey);
    public ITenantScope ForNavLive() => Dataverse.ForTenant("NavLive");
}
