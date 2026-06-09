using System.Linq;
using System.Threading.Tasks;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;
using Tyresoles.Data.Features.Common;

namespace Tyresoles.Data.Features.Purchase;

public class FixedAssetService : IFixedAssetService
{
    private readonly Connector _connector;

    public FixedAssetService(Connector connector)
    {
        _connector = connector;
    }

    public IQueryable<Models.FAClass> GetFAClasses(ITenantScope scope)
        => scope.Query<Models.FAClass>().AsQueryable(scope);

    public IQueryable<Models.FASubclass> GetFASubclasses(ITenantScope scope)
        => scope.Query<Models.FASubclass>().AsQueryable(scope);

    public IQueryable<Models.FixedAsset> GetFixedAssets(ITenantScope scope)
    {
        return scope.Query<Models.FixedAsset>()
            .Join<Dataverse.NavLive.Employee, Models.FixedAsset>(
                fa => fa.ResponsibleEmployee,
                emp => emp.No,
                node => new Models.FixedAsset
                {
                    No = node.Left.No,
                    Description = node.Left.Description,
                    Description2 = node.Left.Description2,
                    ResponsibleEmployee = node.Left.ResponsibleEmployee,
                    ResponsibilityCenter = node.Left.ResponsibilityCenter,
                    FAClassCode = node.Left.FAClassCode,
                    FASubclassCode = node.Left.FASubclassCode,
                    SerialNo = node.Left.SerialNo,
                    VendorNo = node.Left.VendorNo,
                    Blocked = node.Left.Blocked
                },
                JoinType.Left)
            .SelectRaw("ISNULL(t1.[Initials], '') AS [ResponsibleEmployeeInitials]")
            .AsQueryable(scope);
    }

    public IQueryable<Models.FixedAssetServiceLog> GetFixedAssetServiceLogs(ITenantScope scope, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = scope.Query<Models.GLEntry>()
            .Where(l => l.GLAccountNo == "5161" || l.GLAccountNo == "6020");

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.PostingDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(l => l.PostingDate <= toDate.Value);
        }

        return query
            .Join<Models.FixedAsset, JoinQuery<Models.GLEntry, Models.FixedAsset>>(
                l => l.FANo,
                fa => fa.No,
                node => node,
                JoinType.Left)
            .Join<Dataverse.NavLive.Employee, JoinQuery<JoinQuery<Models.GLEntry, Models.FixedAsset>, Dataverse.NavLive.Employee>>(
                node => node.Right.ResponsibleEmployee,
                emp => emp.No,
                node => node,
                JoinType.Left)
            .Join<Dataverse.NavLive.Vendor, Models.FixedAssetServiceLog>(
                node => node.Left.Left.SourceNo,
                v => v.No,
                node => new Models.FixedAssetServiceLog
                {
                    Date = node.Left.Left.Left.PostingDate,
                    Description = node.Left.Left.Left.Description,
                    Location = node.Left.Left.Right.ResponsibilityCenter,
                    RespCenter = node.Left.Left.Right.ResponsibilityCenter,
                    Employee = node.Left.Right.Initials,
                    Class = node.Left.Left.Right.FAClassCode,
                    SubClass = node.Left.Left.Right.FASubclassCode,
                    Amount = node.Left.Left.Left.Amount,
                    VendorNo = node.Left.Left.Left.SourceNo,
                    VendorName = node.Right.Name
                },
                JoinType.Left)
            .AsQueryable(scope);
    }

    public async Task SaveFixedAssetAsync(ITenantScope scope, Models.FixedAsset asset)
    {
        var navAsset = new FixedAsset
        {
            No = asset.No,
            Description = asset.Description,
            Description2 = asset.Description2,
            RespCenter = asset.ResponsibilityCenter,
            Employee = asset.ResponsibleEmployee,
            // PurchaseDate = asset.PurchaseDate, // Assuming DateTime?
            // ExpiryDate = asset.ExpiryDate,
            SerialNo = asset.SerialNo,
            VendorNo = asset.VendorNo,
            Blocked = asset.Blocked == 1,
            Class = asset.FAClassCode,
            SubClass = asset.FASubclassCode,
            // MainAssetNo = asset.MainAssetNo,
            // Inactive = asset.Inactive
        };
        await _connector.UpsertFixedAssetAsync(navAsset);
    }
}
