using Dataverse.NavLive;
using HotChocolate;
using HotChocolate.Resolvers;
using Tyresoles.Data;
using Tyresoles.Data.Features.Purchase;

namespace Tyresoles.Web;

/// <summary>
/// Extends the Vendor type with Balance (sum of Amount from Detailed Vendor Ledger).
/// </summary>
[ExtendObjectType(typeof(Vendor))]
public class VendorTypeExtension
{
    [GraphQLName("balance")]
    public static async Task<decimal> GetBalance(
        [Parent] Vendor vendor,
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        CancellationToken cancellationToken)
    {
        var no = vendor?.No?.Trim();
        if (string.IsNullOrEmpty(no)) return 0;
        // MyVendors hydrates DetailVendorBalance from Detailed Vendor Ledger in SQL — avoid N+1.
        if (vendor.Balance.HasValue)
            return vendor.Balance.Value;
        var scope = dataService.ForTenant("NavLive");
        return await purchaseService.GetVendorBalanceAsync(scope, no, cancellationToken).ConfigureAwait(false);
    }
}
