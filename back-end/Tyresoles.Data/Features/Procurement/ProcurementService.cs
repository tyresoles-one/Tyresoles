using System.Linq;
using Dataverse.NavLive;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;

namespace Tyresoles.Data.Features.Procurement;

public class ProcurementService : IProcurementService
{
    public IQueryable<PurchaseHeader> ProcurementOrders(
        ITenantScope scope,
        string? respCenters = null,
        string? userCode = null,
        string? userDepartment = null,
        string? userSpecialToken = null,
        int? statusFilter = null)
    {
        // 6 usually represents Purchase Return Order, but in Tyresoles.Live it's used for Ecomile Procurement Order
        var query = scope.Query<PurchaseHeader>()
            .Where(h => h.DocumentType == 6);

        //if (!string.IsNullOrEmpty(respCenters))
        //{
        //    query = query.Where(h => h.ResponsibilityCenter == respCenters);
        //}

        if (!string.IsNullOrEmpty(userDepartment) && userDepartment == "Production")
        {
            // Note: In old system it checked if Lines.Inspector == userCode OR Header.Creator == userCode
            // we can filter using userCode directly here if needed or let GraphQL filter do it.
        }
        else if (!string.IsNullOrEmpty(userSpecialToken) && userSpecialToken.ToUpper() == "ECOMGR")
        {
             query = query.Where(h => h.EcomileProcMgr == userCode);
        }

        if (statusFilter.HasValue)
        {
             query = query.Where(h => h.OrderStatus == statusFilter.Value);
        }

        return query.OrderByDescending(h => h.PostingDate).ThenByDescending(h => h.No).AsQueryable(scope);
    }

    public IQueryable<PurchaseLine> ProcurementOrderLines(
        ITenantScope scope,
        string? respCenters = null,
        string? userCode = null,
        string? userDepartment = null,
        string? userSpecialToken = null,
        int? statusFilter = null)
    {
        var query = scope.Query<PurchaseLine>()
            .Where(l => l.DocumentType == 6);

        if (!string.IsNullOrEmpty(respCenters))
        {
             query = query.Where(l => l.ResponsibilityCenter == respCenters);
        }

        if (statusFilter.HasValue && statusFilter.Value != 0)
        {
             query = query.Where(l => l.OrderStatus == statusFilter.Value);
        }

        return query.AsQueryable(scope);
    }

    public IQueryable<ProcurementNewNumberingDto> ProcurementOrderLinesNewNumbering(
        ITenantScope scope,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? respCenters = null,
        string? view = null,
        string? type = null,
        string[]? nos = null,
        string? userCode = null,
        string? userSpecialToken = null)
    {
        var fDate = fromDate ?? DateTime.Now.AddDays(-15).Date;
        var tDate = toDate ?? DateTime.Now.Date;
        var empT = scope.GetQualifiedTableName("Employee", false);

        var query = scope.Query<PurchaseLine>()
            .Where(l => l.DocumentType == 6)
            .Where(l => l.SkipNewNumber == 0)
            .Where(l => l.OrderStatus == 2 || l.OrderStatus == 3 || l.OrderStatus == 4)
            .Where(l => l.DispatchDate >= fDate && l.DispatchDate <= tDate);

        if (!string.IsNullOrEmpty(respCenters))
        {
            // Purchase Line + Vendor JOIN both expose [Responsibility Center]; unqualified [Responsibility Center] in WHERE fails with SQL 209.
            // Root table alias is always t0 (SqlBuilder.RootAlias).
            query = query.Where(@"t0.[Responsibility Center] = @respCenter", new { respCenter = respCenters });
        }
        
        if (type == "Dispatch" && nos != null && nos.Length > 0)
            query = query.Where(l => nos.Contains(l.DispatchOrderNo));

        if (view == "Blank")
            query = query.Where(l => l.NewSerialNo == "");

        if (userSpecialToken == "ECOMGR" && !string.IsNullOrEmpty(userCode) && view == "Posted")
        {
             var vendorT = scope.GetQualifiedTableName("Vendor", false);
             query = query.Where($@"[Buy-from Vendor No_] IN (SELECT [No_] FROM {vendorT} WHERE [Ecomile Proc_ Mgr] = @mgrCode)", new { mgrCode = userCode });
        }

        var txQuery = query
            .Join<Vendor, ProcurementNewNumberingDto>(
                l => l.BuyFromVendorNo,
                v => v.No,
                node => new ProcurementNewNumberingDto
                {
                    OrderNo = node.Left.DocumentNo,
                    LineNo = node.Left.LineNo,
                    No = node.Left.No,
                    Make = node.Left.Make,
                    SerialNo = node.Left.SerialNo,
                    DispatchOrderNo = node.Left.DispatchOrderNo,
                    DispatchDate = node.Left.DispatchDate,
                    DispatchDestination = node.Left.DispatchDestination,
                    DispatchVehicleNo = node.Left.DispatchVehicleNo,
                    DispatchMobileNo = node.Left.DispatchMobileNo,
                    DispatchTransporter = node.Left.DispatchTransporter,
                    Button = node.Left.Button,
                    Model = node.Left.Model,
                    NewSerialNo = node.Left.NewSerialNo,
                    FactInspection = node.Left.Inspection,
                    RejectionReason = node.Left.RejectionReason,
                    Supplier = node.Right.Name ?? "",
                    Location = node.Right.GroupDetails ?? "",
                    Date = node.Left.OrderDate
                }, JoinType.Left)
            // ISNULL: scalar subqueries and CASE without ELSE can yield NULL; GraphQL exposes these as non-nullable String.
            .SelectRaw($@"ISNULL((SELECT [Initials] FROM {empT} WHERE [No_] = t0.[Inspector]), '') AS [Inspector]")
            .SelectRaw($@"ISNULL((SELECT [Initials] FROM {empT} WHERE [No_] = t0.[Fact_ Inspector]), '') AS [FactInspector]")
            .SelectRaw($@"ISNULL((SELECT [Initials] FROM {empT} WHERE [No_] = t0.[Fact_ Inspector Final]), '') AS [FactInspectorFinal]")
            .SelectRaw(
                @"ISNULL(LTRIM(STR(CAST(RIGHT(t0.[Document No_], 5) AS INT))) + ' / ' + LTRIM(STR(t0.[Line No_] / 10000)), '') AS SortNo")
            .SelectRaw(@"CASE t0.[Casing Condition] 
                WHEN 0 THEN '' WHEN 1 THEN 'OK' WHEN 2 THEN 'Superficial Lug damages' WHEN 3 THEN 'Minor Ply damages' 
                WHEN 4 THEN 'Minor one cut upto BP5' WHEN 5 THEN 'Minor two cuts upto BP5' WHEN 6 THEN 'Minor three cuts upto BP5' 
                ELSE '' END AS [Inspection]")
            .SelectRaw(@"CASE t0.[Order Status] 
                WHEN 0 THEN '' WHEN 1 THEN 'Posted' WHEN 2 THEN 'Dispatched' WHEN 3 THEN 'Received At Factory' 
                WHEN 4 THEN 'Purchased' WHEN 5 THEN 'Seconds' WHEN 6 THEN 'Rejected' WHEN 7 THEN 'Dropped' 
                ELSE '' END AS [OrderStatus]")
            .SelectRaw(@"CASE t0.[Order Status] WHEN 5 THEN 'Seconds' WHEN 6 THEN 'Rejected' WHEN 7 THEN 'Dropped' ELSE '' END AS [Remark]");

        return txQuery.AsQueryable(scope);
    }
}
