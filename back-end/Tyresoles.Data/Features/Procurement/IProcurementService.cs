using System.Linq;
using Dataverse.NavLive;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Procurement;

/// <summary>
/// Procurement query service for Ecomile Procurement processes with GraphQL-friendly IQueryable return types.
/// </summary>
public interface IProcurementService
{
    /// <summary>
    /// Returns a query for Procurement Orders (PurchaseHeader DocumentType = 6).
    /// </summary>
    IQueryable<PurchaseHeader> ProcurementOrders(
        ITenantScope scope,
        string? respCenters = null,
        string? userCode = null,
        string? userDepartment = null,
        string? userSpecialToken = null,
        int? statusFilter = null);

    /// <summary>
    /// Returns a query for Procurement Order Lines (PurchaseLine DocumentType = 6).
    /// </summary>
    IQueryable<PurchaseLine> ProcurementOrderLines(
        ITenantScope scope,
        string? respCenters = null,
        string? userCode = null,
        string? userDepartment = null,
        string? userSpecialToken = null,
        int? statusFilter = null);

    /// <summary>
    /// Returns a query for Procurement Order Lines specifically for New Numbering screen.
    /// Ported from legacy Db.Production.ProcurementOrderLinesNewNumbering.
    /// </summary>
    IQueryable<ProcurementNewNumberingDto> ProcurementOrderLinesNewNumbering(
        ITenantScope scope,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? respCenters = null,
        string? view = null,
        string? type = null,
        string[]? nos = null,
        string? userCode = null,
        string? userSpecialToken = null);
}
