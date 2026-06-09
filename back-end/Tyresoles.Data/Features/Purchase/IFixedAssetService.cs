using System.Linq;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Purchase;

public interface IFixedAssetService
{
    IQueryable<Models.FAClass> GetFAClasses(ITenantScope scope);
    IQueryable<Models.FASubclass> GetFASubclasses(ITenantScope scope);
    IQueryable<Models.FixedAsset> GetFixedAssets(ITenantScope scope);
    IQueryable<Models.FixedAssetServiceLog> GetFixedAssetServiceLogs(ITenantScope scope, DateTime? fromDate = null, DateTime? toDate = null);
    Task SaveFixedAssetAsync(ITenantScope scope, Models.FixedAsset asset);
}
