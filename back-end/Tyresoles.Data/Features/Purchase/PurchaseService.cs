using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dataverse.NavLive;
using Tyresoles.Data.Features.Production.Models;
using Tyresoles.Sql;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;

namespace Tyresoles.Data.Features.Purchase;

/// <summary>
/// Purchase-scoped query service for vendors with GraphQL-friendly IQueryable return types.
/// </summary>
public sealed class PurchaseService : IPurchaseService
{
    /// <inheritdoc/>
    public IQueryable<Vendor> MyVendors(
        ITenantScope scope,
        string? respCenter = null,
        string[]? categories = null,
        string? ecoMgr = null)
    {
        var detailedLedger = scope.GetQualifiedTableName("Detailed Vendor Ledg_ Entry", isShared: false);
        // Correlated subquery: same balance as GetVendorBalanceAsync (sum of Amount on Detailed Vendor Ledger).
        var balanceSql =
            $"(SELECT ISNULL(-SUM(d.[Amount]), 0) FROM {detailedLedger} d WITH (NOLOCK) WHERE d.[Vendor No_] = t0.[No_]) AS [Balance]";
        IQuery<Vendor> query = scope.Query<Vendor>()
            .WhereIf(respCenter != null, v => v.ResponsibilityCenter == respCenter)
            .WhereIf(ecoMgr != null, v => v.EcomileProcMgr == ecoMgr)
            .WhereIf(categories != null && categories.Length > 0, v => v.GroupCategory, categories)
            .SelectRaw(balanceSql);
        return query.AsQueryable(scope);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetVendorBalanceAsync(
        ITenantScope scope,
        string vendorNo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vendorNo))
            return 0;
        var sum = await scope.Query<DetailedVendorLedgEntry>()
            .Where(e => e.VendorNo == vendorNo)
            .SumAsync(e => e.Amount, cancellationToken)
            .ConfigureAwait(false);
        return sum;
    }

    /// <inheritdoc/>
    public IQueryable<CasingItem> ItemNos(ITenantScope scope, FetchParams param)
    {
        ArgumentNullException.ThrowIfNull(param);
        param.Regions ??= new();
        param.Areas ??= new();
        param.RespCenters ??= new();

        if (param.Regions.Count > 0 && param.Regions[0] == "CASING" && param.Type == "FromGroupDetail")
        {
            // Must embed `SELECT [Code]` in the IN subquery. Composing IQuery<GroupCategory> as a nested subquery
            // still emitted `SELECT *` (SqlBuilder MemberInit fallback), which SQL Server rejects for IN (...).
            var catT = scope.GetQualifiedTableName("Group Category", false);
            IQuery<GroupDetails> details = scope.Query<GroupDetails>();
            if (param.RespCenters.Count > 0)
            {
                var rc = param.RespCenters[0];
                details = details.Where(
                    $"t0.[Category] IN (SELECT [Code] FROM {catT} WHERE [Type] = 9 AND [Responsibility Center] = @rc)",
                    new { rc });
            }
            else
            {
                details = details.Where(
                    $"t0.[Category] IN (SELECT [Code] FROM {catT} WHERE [Type] = 9)");
            }

            var q = details.Select(g => new CasingItem
            {
                Code = g.Code,
                MinRate = g.Value,
                MaxRate = g.ExtraValue,
                Category = g.Category,
                Name = g.Name
            });
            return q.AsQueryable(scope);
        }

        IQuery<Item> itemQuery = scope.Query<Item>();
        itemQuery = itemQuery.WhereIf(param.Regions.Count > 0, i => i.ItemCategoryCode, param.Regions);
        itemQuery = itemQuery.WhereIf(param.Areas.Count > 0, i => i.ProductGroupCode, param.Areas);

        return itemQuery
            .Select(i => new CasingItem
            {
                Code = i.No,
                MinRate = "",
                MaxRate = "",
                Category = "",
                Name = ""
            })
            .AsQueryable(scope);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Ports <c>Tyresoles.One.Data.Navision.Db.Production.Makes</c>.
    /// Queries Group Details with optional Category filter (param.Regions); when Type == "casing" the
    /// caller-side LINQ filter removes TVS / OTHERS / HARISANCE / DUNLOP / CHINA.
    /// Returned as <see cref="IQueryable{CodeName}"/> so HotChocolate can push paging/sorting/filtering to SQL.
    /// </remarks>
    public IQueryable<CodeName> Makes(ITenantScope scope, FetchParams param)
    {
        ArgumentNullException.ThrowIfNull(param);
        param.Regions ??= new();

        var query = scope.Query<GroupDetails>()
            .WhereIf(param.Regions.Count > 0, g => g.Category, param.Regions)
            .Select(g => new CodeName { Code = g.Code, Name = g.Code });

        var result = query.AsQueryable(scope);

        // Casing filter: exclude specific makes that should not appear in casing procurement UI.
        if (param.Type == "casing")
        {
            var excluded = new[] { "TVS", "OTHERS", "HARISANCE", "DUNLOP", "CHINA" };
            result = result.Where(c => !excluded.Contains(c.Code));
        }

        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Ports <c>Tyresoles.One.Data.Navision.Db.Production.MakeSubMake</c>.
    /// Returns all Group Details rows where Category == param.Type, projecting Category as Code and Code as Name.
    /// </remarks>
    public IQueryable<CodeName> MakeSubMake(ITenantScope scope, FetchParams param)
    {
        ArgumentNullException.ThrowIfNull(param);

        return scope.Query<GroupDetails>()
            .Where(g => g.Category == param.Type)
            .Select(g => new CodeName { Code = g.Category, Name = g.Code })
            .AsQueryable(scope);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Ports <c>Tyresoles.One.Data.Navision.Db.Production.InspectorCodeNames</c>.
    /// No typed ORM model exists for the Employee table in Tyresoles.Sql, so the qualified table name is
    /// resolved via <see cref="ITenantScope.GetQualifiedTableName"/> and the result fetched with
    /// <see cref="ITenantScope.QueryAsync{T}"/> (Dapper-backed). Returns <c>Task&lt;List&lt;CodeName&gt;&gt;</c>
    /// so callers can await and then expose the list via GraphQL UseProjection / in-memory LINQ as needed.
    /// "Factory" type: filter by RespCenters + PROD department. Otherwise: Ecomile Proc Inspector flag = 1.
    /// </remarks>
    public async Task<List<CodeName>> InspectorCodeNamesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(param);
        param.RespCenters ??= new();

        var empT = scope.GetQualifiedTableName("Employee", isShared: false);

        string whereExtra = param.Type == "Factory"
            ? " AND [Responsibility Center] IN @rcs AND [Department] LIKE '%PROD%'"
            : " AND [Ecomile Proc_ Inspector] = 1";

        var sql = $"SELECT [No_] AS Code, [Initials] AS Name FROM {empT} WHERE [Status] = 0{whereExtra}";

        var rows = await scope.QueryAsync<CodeName>(sql, new { rcs = param.RespCenters }, ct).ConfigureAwait(false);
        return rows.ToList();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Pure in-memory static list — no database call. Mirrors the fixed set of casing inspection
    /// conditions defined in <c>Tyresoles.One.Data.Navision.Db.Production.ProcurementInspection</c>.
    /// </remarks>
    public List<CodeName> ProcurementInspection() => new()
    {
        new() { Code = "",                          Name = "" },
        new() { Code = "OK",                         Name = "OK" },
        new() { Code = "Superficial Lug damages",    Name = "Superficial Lug damages" },
        new() { Code = "Minor Ply damages",          Name = "Minor Ply damages" },
        new() { Code = "Minor one cut upto BP5",     Name = "Minor one cut upto BP5" },
        new() { Code = "Minor two cuts upto BP5",    Name = "Minor two cuts upto BP5" },
        new() { Code = "Minor three cuts upto BP5",  Name = "Minor three cuts upto BP5" },
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Ports <c>Tyresoles.One.Data.Navision.Db.Production.ProcurementMarkets</c>.
    /// Fetches Code (Code as Name) from Group Details where Category == 'CASING PROCUREMENT'.
    /// </remarks>
    public IQueryable<CodeName> ProcurementMarkets(ITenantScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return scope.Query<GroupDetails>()
            .Where(g => g.Category == "CASING PROCUREMENT")
            .Select(g => new CodeName { Code = g.Code, Name = g.Code })
            .AsQueryable(scope);
    }
}
