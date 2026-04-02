using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dataverse.NavLive;
using Tyresoles.Data.Features.Production.Models;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Purchase;

/// <summary>
/// Purchase-scoped query service for vendors with GraphQL-friendly IQueryable return types.
/// </summary>
public interface IPurchaseService
{
    /// <summary>
    /// Returns a query for vendors filtered by responsibility center, ecomile procurement manager, and categories.
    /// Each row includes <see cref="Vendor.DetailVendorBalance"/> (sum of <see cref="DetailedVendorLedgEntry.Amount"/> per vendor).
    /// Fully compatible with GraphQL projection, sorting, filters, and paging.
    /// </summary>
    IQueryable<Vendor> MyVendors(
        ITenantScope scope,
        string? respCenter = null,
        string[]? categories = null,
        string? ecoMgr = null);

    /// <summary>
    /// Returns the balance (sum of Amount) from Detailed Vendor Ledger for the given vendor.
    /// </summary>
    Task<decimal> GetVendorBalanceAsync(
        ITenantScope scope,
        string vendorNo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Item / casing numbers (port of legacy Db.Production.ItemNos). Branch CASING + FromGroupDetail uses Group Category / Group Details;
    /// otherwise Item with optional item category and product group filters. Returned as an <c>IQueryable</c> of <see cref="CasingItem"/> for GraphQL paging, filtering, and sorting.
    /// </summary>
    IQueryable<CasingItem> ItemNos(ITenantScope scope, FetchParams param);

    /// <summary>
    /// Returns tyre makes (Code, Code as Name) from Group Details filtered by <paramref name="param"/>.Regions (Categories).
    /// When <see cref="FetchParams.Type"/> == "casing", excludes TVS / OTHERS / HARISANCE / DUNLOP / CHINA.
    /// Port of legacy <c>Db.Production.Makes</c>.
    /// </summary>
    IQueryable<CodeName> Makes(ITenantScope scope, FetchParams param);

    /// <summary>
    /// Returns sub-make list (Category as Code, Code as Name) from Group Details where Category == <see cref="FetchParams.Type"/>.
    /// Port of legacy <c>Db.Production.MakeSubMake</c>.
    /// </summary>
    IQueryable<CodeName> MakeSubMake(ITenantScope scope, FetchParams param);

    /// <summary>
    /// Returns inspector / procurement inspector names from the Employee table.
    /// When <see cref="FetchParams.Type"/> == "Factory", filters by RespCenters and PROD department;
    /// otherwise returns employees flagged as Ecomile Proc Inspector.
    /// Port of legacy <c>Db.Production.InspectorCodeNames</c>.
    /// </summary>
    Task<List<CodeName>> InspectorCodeNamesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);

    /// <summary>
    /// Returns the fixed static list of casing inspection conditions (no DB call).
    /// Port of legacy <c>Db.Production.ProcurementInspection</c>.
    /// </summary>
    List<CodeName> ProcurementInspection();

    /// <summary>
    /// Returns procurement market codes from Group Details where Category == 'CASING PROCUREMENT'.
    /// Port of legacy <c>Db.Production.ProcurementMarkets</c>.
    /// </summary>
    IQueryable<CodeName> ProcurementMarkets(ITenantScope scope);
}
