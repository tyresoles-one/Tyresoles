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
        => scope.Query<Models.FixedAsset>().AsQueryable(scope);

    public IQueryable<Models.FixedAssetServiceLog> GetFixedAssetServiceLogs(ITenantScope scope)
    {
        // GLEntry where GLAccountNo in 5161, 6020. (IQuery<T> does not support GroupJoin/join-into for left joins; enrich later if needed.)
        var query = from ledger in scope.Query<Models.GLEntry>()
                    where ledger.GLAccountNo == "5161" || ledger.GLAccountNo == "6020"
                    select new Models.FixedAssetServiceLog
                    {
                        Date = ledger.PostingDate,
                        Description = ledger.Description,
                        Amount = ledger.Amount,
                        VendorNo = ledger.SourceNo,
                    };
        return query.AsQueryable(scope);
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
