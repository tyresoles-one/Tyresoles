using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tyresoles.Data.Features.Production.Models;
using Tyresoles.Sql.Abstractions;
using SoapVendor = Tyresoles.Data.Features.Common.Vendor;
using SoapOrderInfo = Procurement.OrderInfo;
using SoapOrderLine = Procurement.OrderLine;
using SoapOrderLineDispatch = Procurement.OrderLineDispatch;
using SoapFetchParams = Tyresoles.Data.Features.Common.FetchParams;
using Connector = Tyresoles.Data.Features.Common.Connector;
using CodeName = Tyresoles.Data.Features.Production.Models.CodeName;
using Tile = Tyresoles.Data.Features.Production.Models.Tile;

namespace Tyresoles.Data.Features.Production;

public sealed class ProductionService : IProductionService
{
    private readonly ILogger<ProductionService> _logger;
    private readonly Connector _connector;

    public ProductionService(ILogger<ProductionService> logger, Connector connector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
    }

    #region Masters

    public async Task<List<CasingItem>> GetItemNosAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        if (param.Regions.Any() && param.Regions[0] == "CASING" && param.Type == "FromGroupDetail")
        {
            var catT = scope.GetQualifiedTableName("Group Category", false);
            var detailT = scope.GetQualifiedTableName("Group Details", false);

            var sql = $@"
SELECT [Code], [Value] as MinRate, [Extra Value] as MaxRate, [Category]
FROM {detailT}
WHERE [Category] IN (SELECT [Code] FROM {catT} WHERE [Type] = 9 {(param.RespCenters.Any() ? "AND [Responsibility Center] = @rc" : "")})";

            var result = await scope.QueryAsync<CasingItem>(sql, new { rc = param.RespCenters.FirstOrDefault() }, ct);
            return result.ToList();
        }
        else
        {
            var itemT = scope.GetQualifiedTableName("Item", false);
            var whereCategory = param.Regions.Any() ? " WHERE [Item Category Code] IN @regions" : "";
            var whereGroup = param.Areas.Any() ? (string.IsNullOrEmpty(whereCategory) ? " WHERE " : " AND ") + "[Product Group Code] IN @groups" : "";

            var sql = $@"SELECT [No_] as Code FROM {itemT} {whereCategory} {whereGroup}";
            var result = await scope.QueryAsync<CasingItem>(sql, new { regions = param.Regions, groups = param.Areas }, ct);
            return result.ToList();
        }
    }

    public async Task UpdateCasingAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        if (param.Nos.Any() && param.RespCenters.Any())
        {
            await DeleteCasingOfRespCenterInternalAsync(scope, param.RespCenters[0], ct);
            string categoryCode = param.RespCenters[0] switch
            {
                "BEL" => "BCASING",
                "JBP" => "JCASING",
                "AHM" => "ACASING",
                _ => ""
            };

            var detailT = scope.GetQualifiedTableName("Group Details", false);
            foreach (var no in param.Nos)
            {
                var sql = $@"INSERT INTO {detailT} ([Code], [Category], [Name], [Value], [Extra Value]) VALUES (@Code, @Category, @Name, @Value, @ExtraValue)";
                await scope.ExecuteNonQueryAsync(sql, new { Code = no, Category = categoryCode, Name = no, Value = "", ExtraValue = "" }, ct);
            }
        }
    }

    public async Task InsertCasingItemsAsync(ITenantScope scope, List<CasingItem> casingItems, CancellationToken ct = default)
    {
        if (casingItems == null || !casingItems.Any()) return;

        var detailT = scope.GetQualifiedTableName("Group Details", false);
        var categories = casingItems.Select(c => c.Category).Distinct().ToList();

        var deleteSql = $@"DELETE FROM {detailT} WHERE [Category] IN @categories";
        await scope.ExecuteNonQueryAsync(deleteSql, new { categories }, ct);

        foreach (var item in casingItems)
        {
            var insertSql = $@"INSERT INTO {detailT} ([Code], [Category], [Name], [Value], [Extra Value]) VALUES (@Code, @Category, @Name, @Value, @ExtraValue)";
            await scope.ExecuteNonQueryAsync(insertSql, new { Code = item.Code, Category = item.Category, Name = item.Code, Value = item.MinRate, ExtraValue = item.MaxRate }, ct);
        }
    }

    private async Task<int> DeleteCasingOfRespCenterInternalAsync(ITenantScope scope, string respCenter, CancellationToken ct)
    {
        var catT = scope.GetQualifiedTableName("Group Category", false);
        var detailT = scope.GetQualifiedTableName("Group Details", false);

        var sql = $@"
DELETE FROM {detailT}
WHERE [Category] IN (SELECT [Code] FROM {catT} WHERE [Type] = 9 AND [Responsibility Center] = @respCenter)";

        return await scope.ExecuteNonQueryAsync(sql, new { respCenter }, ct);
    }

    public async Task<List<CodeName>> GetMakesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var detailT = scope.GetQualifiedTableName("Group Details", false);
        var where = param.Regions.Any() ? " WHERE [Category] IN @regions" : "";
        var sql = $@"SELECT [Code], [Code] as Name FROM {detailT} {where}";

        var result = await scope.QueryAsync<CodeName>(sql, new { regions = param.Regions }, ct);
        var list = result.ToList();

        if (param.Type == "casing")
        {
            var excludeCodes = new[] { "TVS", "OTHERS", "HARISANCE", "DUNLOP", "CHINA" };
            list = list.Where(c => !excludeCodes.Contains(c.Code)).ToList();
        }
        return list;
    }

    public async Task<List<CodeName>> GetMakeSubMakeAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var detailT = scope.GetQualifiedTableName("Group Details", false);
        var sql = $@"SELECT [Category] as Code, [Code] as Name FROM {detailT} WHERE [Category] = @type";
        var result = await scope.QueryAsync<CodeName>(sql, new { type = param.Type }, ct);
        return result.ToList();
    }

    public async Task<List<CodeName>> GetVendorsCodeNamesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var vendorT = scope.GetQualifiedTableName("Vendor", false);
        var whereCat = param.Regions.Any() ? " AND [Group Category] IN @regions" : "";
        var sql = $@"
SELECT [No_] as Code, [Name] + ' ('+[City]+') '+LEFT([No_],1)+RIGHT([No_],4) as Name
FROM {vendorT}
WHERE [Blocked] = 0 {whereCat}";

        var result = await scope.QueryAsync<CodeName>(sql, new { regions = param.Regions }, ct);
        return result.ToList();
    }

    public async Task<List<CodeName>> GetInspectorCodeNamesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var empT = scope.GetQualifiedTableName("Employee", false);
        var sql = $"SELECT [No_] as Code, [Initials] as Name FROM {empT} WHERE [Status] = 0";

        if (param.Type == "Factory")
        {
            sql += " AND [Responsibility Center] IN @rcs AND [Department] LIKE '%PROD%'";
        }
        else
        {
            sql += " AND [Ecomile Proc_ Inspector] = 1";
        }

        var result = await scope.QueryAsync<CodeName>(sql, new { rcs = param.RespCenters }, ct);
        return result.ToList();
    }

    public List<CodeName> GetProcurementInspection(FetchParams param)
    {
        return new List<CodeName>
        {
            new() { Code = "", Name = "" },
            new() { Code = "OK", Name = "OK" },
            new() { Code = "Superficial Lug damages", Name = "Superficial Lug damages" },
            new() { Code = "Minor Ply damages", Name = "Minor Ply damages" },
            new() { Code = "Minor one cut upto BP5", Name = "Minor one cut upto BP5" },
            new() { Code = "Minor two cuts upto BP5", Name = "Minor two cuts upto BP5" },
            new() { Code = "Minor three cuts upto BP5", Name = "Minor three cuts upto BP5" }
        };
    }

    public async Task<List<CodeName>> GetProcurementMarketsAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var detailT = scope.GetQualifiedTableName("Group Details", false);
        var sql = $@"SELECT [Code], [Code] as Name FROM {detailT} WHERE [Category] = 'CASING PROCUREMENT'";
        var result = await scope.QueryAsync<CodeName>(sql, null, ct);
        return result.ToList();
    }

    public async Task<string> CreateVendorAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        return await _connector.CreateVendorAsync(param.RespCenters.FirstOrDefault() ?? "", param.UserCode, param.UserSpecialToken == "ECOMGR" ? param.UserCode : "");
    }

    public async Task<bool> UpdateVendorAsync(ITenantScope scope, VendorModel param, CancellationToken ct = default)
    {
        // Map VendorModel to SOAP Vendor model
        var navVendor = new SoapVendor
        {
            No = param.No,
            RespCenter = param.RespCenter,
            Name = param.Name,
            Address = param.Address,
            Address2 = param.Address2,
            City = param.City,
            StateCode = param.StateCode,
            MobileNo = param.MobileNo,
            Category = param.Category,
            Detail = param.Detail,
            EcoMgrCode = param.EcoMgrCode,
            SelfInvoice = param.SelfInvoice,
            NameOnInvoice = param.NameOnInvoice,
            BankName = param.BankName,
            BankAccNo = param.BankAccNo,
            BankIFSC = param.BankIFSC,
            BankBranch = param.BankBranch,
            PanNo = param.PanNo,
            AdhaarNo = param.AdhaarNo
        };
        return await _connector.UpdateVendorAsync(navVendor);
    }

    public async Task<List<VendorModel>> GetVendorsAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var vendT = scope.GetQualifiedTableName("Vendor", false);
        var ledgerT = scope.GetQualifiedTableName("Detailed Vendor Ledg_ Entry", false);

        var whereMgr = param.UserSpecialToken == "ECOMGR" ? " AND [Ecomile Proc_ Mgr] = @userCode" : "";
        var whereRC = param.RespCenters.Any() ? " AND [Responsibility Center] IN @rcs" : "";
        var whereReg = param.Regions.Any() ? " AND [Group Category] IN @regions" : "";

        var having = param.View switch
        {
            "Debit" => " HAVING ISNULL(-Sum(Ledger.[Amount]),0) > 0",
            "Credit" => " HAVING ISNULL(-Sum(Ledger.[Amount]),0) < 0",
            "Non Zero" => " HAVING ISNULL(-Sum(Ledger.[Amount]),0) != 0",
            _ => ""
        };

        var sql = $@"
SELECT 
    Vend.[No_] as No, Vend.[Name], Vend.[Address], Vend.[Address 2] as Address2, Vend.[City],
    Vend.[Group Category] as Category, Vend.[Group Details] as Detail, Vend.[Responsibility Center] as RespCenter,
    Vend.[Phone No_] as MobileNo, Vend.[Ecomile Proc_ Mgr] as EcoMgrCode, Vend.[Name on Invoice] as NameOnInvoice,
    Vend.[Bank Name] as BankName, Vend.[Bank IFSC Code] as BankIFSC, Vend.[Bank A_c No] as BankAccNo, Vend.[Post Code] as PostCode,
    Vend.[Bank Branch] as BankBranch, Vend.[Self Invoice] as SelfInvoice, Vend.[P_A_N_ No_] as PanNo, Vend.[Adhaar No_] as AdhaarNo,
    ISNULL(-Sum(Ledger.[Amount]), 0) as Balance,
    ISNULL(Vend.[State Code], '') as StateCode
FROM {vendT} as Vend
LEFT JOIN {ledgerT} as Ledger ON Ledger.[Vendor No_] = Vend.[No_]
WHERE Vend.[Blocked] = 0 {whereMgr} {whereRC} {whereReg}
GROUP BY 
    Vend.[No_], Vend.[Name], Vend.[Address], Vend.[Address 2], Vend.[City], Vend.[State Code], Vend.[Group Category], Vend.[Post Code],
    Vend.[Group Details], Vend.[Responsibility Center], Vend.[Phone No_], Vend.[Ecomile Proc_ Mgr], Vend.[Name on Invoice],
    Vend.[Bank Name], Vend.[Bank IFSC Code], Vend.[Bank A_c No], Vend.[Bank Branch], Vend.[Self Invoice], Vend.[P_A_N_ No_], Vend.[Adhaar No_]
{having}";

        var result = await scope.QueryAsync<VendorModel>(sql, new { userCode = param.UserCode, rcs = param.RespCenters, regions = param.Regions }, ct);
        return result.ToList();
    }

    #endregion

    #region Ecomile Operations

    public async Task<string> GetEcomileLastNewNumberAsync(ITenantScope scope, string respCenter, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(respCenter)) return "";

        var ecomileTable = scope.GetQualifiedTableName("Ecomile Items_", false);
        var purLineTable = scope.GetQualifiedTableName("Purchase Line", false);
        string splitter = "0300";
        string prefix = respCenter.Substring(0, 1);

        var sql1 = $@"SELECT MAX([New Serial No_]) FROM {ecomileTable} WHERE [Responsibility Center] = @rc AND [New Serial No_] LIKE @like";
        var sql2 = $@"SELECT MAX([New Serial No_]) FROM {purLineTable} WHERE [Responsibility Center] = @rc AND [New Serial No_] LIKE @like";

        var like = $"{prefix}{splitter}%";
        var fromEcomile = await scope.ExecuteScalarAsync<string>(sql1, new { rc = respCenter, like }, ct);
        var fromPurLine = await scope.ExecuteScalarAsync<string>(sql2, new { rc = respCenter, like }, ct);

        int intEcomile = 0;
        if (!string.IsNullOrEmpty(fromEcomile) && fromEcomile.Contains(splitter))
            int.TryParse(fromEcomile.Split(splitter)[1], out intEcomile);

        int intPurLine = 0;
        if (!string.IsNullOrEmpty(fromPurLine) && fromPurLine.Contains(splitter))
            int.TryParse(fromPurLine.Split(splitter)[1], out intPurLine);

        return intEcomile > intPurLine ? fromEcomile ?? "" : fromPurLine ?? "";
    }

    public async Task<List<OrderInfo>> GetProcurementOrdersInfoAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var headerT = scope.GetQualifiedTableName("Purchase Header", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);
        var empT = scope.GetQualifiedTableName("Employee", false);
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);

        var statusMap = new Dictionary<string, int> { { "Posted", 1 }, { "Dispatched", 2 }, { "Received At Factory", 3 }, { "Purchased", 4 } };
        int statusVal = param.View != null && statusMap.TryGetValue(param.View, out var v) ? v : 0;

        var whereUser = "";
        if (param.UserDepartment == "Production")
            whereUser += " AND (Lines.[Inspector] = @userCode OR Header.[Proc_ Creator] = @userCode)";
        if (param.UserSpecialToken?.ToUpper() == "ECOMGR")
            whereUser += " AND (Header.[Ecomile Proc_ Mgr] = @userCode OR Header.[Proc_ Creator] = @userCode)";

        var sql = $@"
SELECT 
    Header.[No_] as OrderNo,
    Header.[Buy-from Vendor Name] as Supplier,
    Header.[Buy-from Vendor No_] as SupplierCode,
    Header.[Responsibility Center] as RespCenter,
    Header.[Order Status] as Status,
    ISNULL(Vendors.[Group Details],'') as Location,
    Header.[Ecomile Proc_ Mgr] as ManagerCode,
    FORMAT(Header.[Posting Date], 'dd-MMM-yy') as Date,
    ISNULL(Employees.[Initials],'') as Manager,
    SUM(ISNULL(Lines.[Quantity],0)) as Qty,
    SUM(ISNULL(Lines.[Direct Unit Cost],0)) as Amount
FROM {headerT} as Header
LEFT JOIN {vendorT} as Vendors ON Vendors.[No_] = Header.[Buy-from Vendor No_]
LEFT JOIN {empT} as Employees ON Employees.[No_] = Header.[Ecomile Proc_ Mgr]
LEFT JOIN {lineT} as Lines ON Lines.[Document No_] = Header.[No_]
WHERE Header.[Document Type] = 6 AND Header.[Order Status] = @status {whereUser}
GROUP BY 
    Header.[No_], Header.[Buy-from Vendor No_], Header.[Buy-from Vendor Name], Header.[Responsibility Center], 
    Header.[Ecomile Proc_ Mgr], Header.[Posting Date], Employees.[Initials], Vendors.[Group Details], Header.[Order Status]
ORDER BY Header.[Posting Date] DESC, Header.[No_] DESC";

        var result = await scope.QueryAsync<OrderInfo>(sql, new { status = statusVal, userCode = param.UserCode, rcs = param.RespCenters }, ct);
        return result.ToList();
    }

    /// <inheritdoc cref="IProductionService.GetProcurementOrderLinesDispatchAsync(ITenantScope, FetchParams, CancellationToken)" />
    public async Task<List<OrderLineDispatch>> GetProcurementOrderLinesDispatchAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(param);
        return await GetProcurementOrderLinesDispatchAsync(
            scope,
            param.View,
            param.Type,
            param.UserCode,
            param.Nos?.ToArray() ?? Array.Empty<string>(),
            param.RespCenters?.ToArray() ?? Array.Empty<string>(),
            param.UserSpecialToken,
            ct).ConfigureAwait(false);
    }

    public async Task<List<OrderLineDispatch>> GetProcurementOrderLinesDispatchAsync(ITenantScope scope, string? view, string? type, string? enitityCode, string[] nos, string[] respCenters,
        string? userSpecialToken = null, CancellationToken ct = default)
    {
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);
        var empT = scope.GetQualifiedTableName("Employee", false);

        var whereStatus = "";
        if (view == "Posted") whereStatus = " AND Line.[Order Status] = 1";
        else if (view == "Dispatched") whereStatus = " AND Line.[Order Status] = 2";
        else if (view == "Recieved") whereStatus = " AND Line.[Order Status] = 3";
        else if (view == "Purchased") whereStatus = " AND Line.[Order Status] = 4";

        var whereDispatch = type == "Dispatch" && nos.Any() ? " AND Line.[Dispatch Order No_] IN @nos" : "";
        var whereRC = respCenters.Any() ? " AND Line.[Responsibility Center] IN @rcs" : "";
        
        var whereMgr = "";
        if (userSpecialToken == "ECOMGR" && !string.IsNullOrEmpty(enitityCode) && view == "Posted")
        {
            whereMgr = $" AND Line.[Buy-from Vendor No_] IN (SELECT [No_] FROM {vendorT} WHERE [Ecomile Proc_ Mgr] = @userCode)";
        }

        var sql = $@"
SELECT 
    Line.[Document No_] AS [OrderNo], Line.[Line No_] AS [LineNo], Line.[No_] AS [No], Line.[Make], Line.[Serial No_] AS [SerialNo],
    Line.[Dispatch Order No_] AS [DispatchOrderNo], Line.[Dispatch Date] AS [DispatchDate], Line.[Dispatch Destination] AS [DispatchDestination],
    Line.[Dispatch Vehicle No_] AS [DispatchVehicleNo], Line.[Dispatch Mobile No] AS [DispatchMobileNo], Line.[Dispatch Transporter] AS [DispatchTransporter],
    Line.[Button], Line.[Model], Line.[Inspection] AS [FactInspection], Line.[New Serial No_] AS [NewSerialNo], Line.[Rejection Reason] AS [RejectionReason],
    Vend.[Name] AS [Supplier], Vend.[Group Details] AS [Location],
    FORMAT(Line.[Order Date], 'dd-MMM-yy') AS [Date],
    LTRIM(STR(CAST(RIGHT(Line.[Document No_], 5) AS INT))) + ' / ' + LTRIM(STR(Line.[Line No_] / 10000)) AS [SortNo],
    CASE Line.[Casing Condition] 
        WHEN 0 THEN '' WHEN 1 THEN 'OK' WHEN 2 THEN 'Superficial Lug damages' WHEN 3 THEN 'Minor Ply damages' 
        WHEN 4 THEN 'Minor one cut upto BP5' WHEN 5 THEN 'Minor two cuts upto BP5' WHEN 6 THEN 'Minor three cuts upto BP5' 
    END AS [Inspection],
    CASE Line.[Order Status] 
        WHEN 0 THEN '' WHEN 1 THEN 'Posted' WHEN 2 THEN 'Dispatched' WHEN 3 THEN 'Received At Factory' 
        WHEN 4 THEN 'Purchased' WHEN 5 THEN 'Seconds' WHEN 6 THEN 'Rejected' WHEN 7 THEN 'Dropped' WHEN 8 THEN 'Returned' 
    END AS [OrderStatus],
    ISNULL(E1.[Initials],'') AS [Inspector],
    ISNULL(E2.[Initials],'') AS [FactInspector],
    ISNULL(E3.[Initials],'') AS [FactInspectorFinal],
    CASE Line.[Order Status] WHEN 5 THEN 'Seconds' WHEN 6 THEN 'Rejected' WHEN 7 THEN 'Dropped' WHEN 8 THEN 'Returned' ELSE '' END AS [Remark]
FROM {lineT} AS Line
LEFT JOIN {vendorT} AS Vend ON Vend.[No_] = Line.[Buy-from Vendor No_]
LEFT JOIN {empT} AS E1 ON E1.[No_] = Line.[Inspector]
LEFT JOIN {empT} AS E2 ON E2.[No_] = Line.[Fact_ Inspector]
LEFT JOIN {empT} AS E3 ON E3.[No_] = Line.[Fact_ Inspector Final]
WHERE Line.[Document Type] = 6 {whereStatus} {whereDispatch} {whereRC} {whereMgr}";

        var result = await scope.QueryAsync<OrderLineDispatch>(sql, new { nos = nos, rcs = respCenters, userCode = enitityCode }, ct);
        return result.ToList();
    }

    public async Task<List<OrderLineDispatch>> GetProcurementOrderLinesNewNumberingAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        string lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);
        var empT = scope.GetQualifiedTableName("Employee", false);

        var whereDate = (param.From != null && param.To != null) 
            ? " AND Line.[Dispatch Date] BETWEEN @from AND @to"
            : " AND Line.[Dispatch Date] BETWEEN @defFrom AND @defTo";

        var whereRC = param.RespCenters.Any() ? " AND Line.[Responsibility Center] IN @rcs" : "";
        var whereDispatch = param.Type == "Dispatch" && param.Nos.Any() ? " AND Line.[Dispatch Order No_] IN @nos" : "";
        var whereBlank = param.View == "Blank" ? " AND Line.[New Serial No_] = ''" : "";

        var whereMgr = "";
        if (param.UserSpecialToken == "ECOMGR" && !string.IsNullOrEmpty(param.UserCode) && param.View == "Posted")
        {
            whereMgr = $" AND Line.[Buy-from Vendor No_] IN (SELECT [No_] FROM {vendorT} WHERE [Ecomile Proc_ Mgr] = @userCode)";
        }

        var sql = $@"
SELECT 
    Line.[Document No_] AS [OrderNo], Line.[Line No_] AS [LineNo], Line.[No_] AS [No], Line.[Make], Line.[Serial No_] AS [SerialNo],
    Line.[Dispatch Order No_] AS [DispatchOrderNo], Line.[Dispatch Date] AS [DispatchDate], Line.[Dispatch Destination] AS [DispatchDestination],
    Line.[Dispatch Vehicle No_] AS [DispatchVehicleNo], Line.[Dispatch Mobile No] AS [DispatchMobileNo], Line.[Dispatch Transporter] AS [DispatchTransporter],
    Line.[Button], Line.[Model], Line.[Inspection] AS [FactInspection], Line.[New Serial No_] AS [NewSerialNo], Line.[Rejection Reason] AS [RejectionReason],
    Vend.[Name] AS [Supplier], Vend.[Group Details] AS [Location],
    LTRIM(STR(CAST(RIGHT(Line.[Document No_], 5) AS INT))) + ' / ' + LTRIM(STR(Line.[Line No_] / 10000)) AS [SortNo],
    FORMAT(Line.[Order Date], 'dd-MMM-yy') AS [Date],
    CASE Line.[Casing Condition] 
        WHEN 0 THEN '' WHEN 1 THEN 'OK' WHEN 2 THEN 'Superficial Lug damages' WHEN 3 THEN 'Minor Ply damages' 
        WHEN 4 THEN 'Minor one cut upto BP5' WHEN 5 THEN 'Minor two cuts upto BP5' WHEN 6 THEN 'Minor three cuts upto BP5' 
    END AS [Inspection],
    CASE Line.[Order Status] 
        WHEN 0 THEN '' WHEN 1 THEN 'Posted' WHEN 2 THEN 'Dispatched' WHEN 3 THEN 'Received At Factory' 
        WHEN 4 THEN 'Purchased' WHEN 5 THEN 'Seconds' WHEN 6 THEN 'Rejected' WHEN 7 THEN 'Dropped' 
    END AS [OrderStatus],
    ISNULL(E1.[Initials],'') AS [Inspector],
    ISNULL(E2.[Initials],'') AS [FactInspector],
    ISNULL(E3.[Initials],'') AS [FactInspectorFinal],
    CASE Line.[Order Status] WHEN 5 THEN 'Seconds' WHEN 6 THEN 'Rejected' WHEN 7 THEN 'Dropped' ELSE '' END AS [Remark]
FROM {lineT} AS Line
LEFT JOIN {vendorT} AS Vend ON Vend.[No_] = Line.[Buy-from Vendor No_]
LEFT JOIN {empT} AS E1 ON E1.[No_] = Line.[Inspector]
LEFT JOIN {empT} AS E2 ON E2.[No_] = Line.[Fact_ Inspector]
LEFT JOIN {empT} AS E3 ON E3.[No_] = Line.[Fact_ Inspector Final]
WHERE Line.[Document Type] = 6 AND Line.[Skip New Number] = 0 AND Line.[Order Status] IN (2,3,4)
{whereDate} {whereRC} {whereDispatch} {whereBlank} {whereMgr}";

        var defFrom = DateTime.Now.AddDays(-15).Date;
        var defTo = DateTime.Now.Date;

        var result = await scope.QueryAsync<OrderLineDispatch>(sql, new 
        { 
            from = param.From, to = param.To, defFrom, defTo, rcs = param.RespCenters, nos = param.Nos, userCode = param.UserCode 
        }, ct);
        return result.ToList();
    }

    /// <summary>
    /// Ported from Live <c>Tyresoles.One.Data.Navision.Db.Production.ProcurementOrderLines</c> in <c>Db.Production.cs</c>
    /// (purchase line columns and employee join; filter by document number only, matching legacy).
    /// </summary>
    public async Task<List<OrderLine>> GetProcurementOrderLinesAsync(ITenantScope scope, OrderInfo param, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(param.OrderNo))
            return new List<OrderLine>();

        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var empT = scope.GetQualifiedTableName("Employee", false);

        var sql = $@"
SELECT 
    Line.[Document No_] AS [No], Line.[Line No_] AS [LineNo], Line.[Buy-from Vendor No_] AS [VendorNo],
    Line.[No_] AS [ItemNo], Line.[Make], Line.[Serial No_] AS [SerialNo], Line.[Direct Unit Cost] AS [Amount],
    Line.[Sub Make] AS [SubMake], Line.[Inspector] AS [InspectorCode],
    LTRIM(STR(CAST(RIGHT(Line.[Document No_], 5) AS INT))) + ' / ' + LTRIM(STR(Line.[Line No_] / 10000)) AS [SortNo],
    CASE Line.[Casing Condition] 
        WHEN 0 THEN '' WHEN 1 THEN 'OK' WHEN 2 THEN 'Superficial Lug damages' WHEN 3 THEN 'Minor Ply damages' 
        WHEN 4 THEN 'Minor one cut upto BP5' WHEN 5 THEN 'Minor two cuts upto BP5' WHEN 6 THEN 'Minor three cuts upto BP5' 
    END AS [Inspection],
    ISNULL(E.[Initials],'') AS [Inspector]
FROM {lineT} AS Line
LEFT JOIN {empT} AS E ON E.[No_] = Line.[Inspector]
WHERE Line.[Document No_] = @orderNo";

        var result = await scope.QueryAsync<OrderLine>(sql, new { orderNo = param.OrderNo }, ct);
        return result.ToList();
    }

    public async Task<List<DispatchOrder>> GetProcurementDispatchOrdersAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);

        var whereMgr = "";
        if (param.UserSpecialToken == "ECOMGR" && !string.IsNullOrEmpty(param.UserCode))
        {
            whereMgr = $" AND [Buy-from Vendor No_] IN (SELECT [No_] FROM {vendorT} WHERE [Ecomile Proc_ Mgr] = @userCode)";
        }

        var sql = $@"
SELECT 
    [Dispatch Order No_] as No, [Dispatch Mobile No] as MobileNo, [Dispatch Vehicle No_] as VehicleNo,
    [Dispatch Destination] as Destination, FORMAT([Dispatch Date], 'dd-MMM-yy') as Date,
    COUNT([Quantity]) as Tyres,
    CASE [Order Status] WHEN 0 THEN '' WHEN 1 THEN 'Posted' WHEN 2 THEN 'Dispatched' WHEN 3 THEN 'Received At Factory' WHEN 4 THEN 'Purchased' END as Status
FROM {lineT}
WHERE [Document Type] = 6 AND [Order Status] <= 5 AND [Dispatch Order No_] <> ''
{(param.RespCenters.Any() ? " AND [Responsibility Center] IN @rcs" : "")}
{whereMgr}
GROUP BY [Dispatch Order No_], [Dispatch Destination], [Dispatch Date], [Dispatch Mobile No], [Dispatch Vehicle No_], [Order Status]
ORDER BY [Dispatch Date] DESC";

        var result = await scope.QueryAsync<DispatchOrder>(sql, new { rcs = param.RespCenters, userCode = param.UserCode }, ct);
        return result.ToList();
    }

    /// <summary>
    /// Ported from Live <c>Tyresoles.One.Data.Navision.Db.Production.NewProcurementOrder</c> (<c>Db.Production.cs</c>):
    /// delegates to NAV SOAP via <c>Connector.NewProcurementOrderAsync</c> (new purchase header document number).
    /// </summary>
    public async Task<string> NewProcurementOrderAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        _ = scope;
        var soap = new SoapFetchParams
        {
            RespCenters = param.RespCenters,
            UserCode = param.UserCode,
            UserDepartment = param.UserDepartment,
            UserType = param.UserType,
            UserSpecialToken = param.UserSpecialToken,
            Regions = param.Regions,
            Areas = param.Areas,
            Nos = param.Nos,
            Type = param.Type,
            View = param.View,
            From = param.From,
            To = param.To,
            ReportName = param.ReportName,
        };
        return await _connector.NewProcurementOrderAsync(soap);
    }

    public async Task<List<string>> GetProcMarketsAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        if (param.UserSpecialToken == "ECOMGR" && !string.IsNullOrEmpty(param.UserCode))
        {
            var vendT = scope.GetQualifiedTableName("Vendor", false);
            var sql = $@"SELECT [Group Details] FROM {vendT} WHERE [Group Category] = 'CASING PROCUREMENT' AND [Ecomile Proc_ Mgr] = @userCode GROUP BY [Group Details]";
            var result = await scope.QueryAsync<string>(sql, new { userCode = param.UserCode }, ct);
            return result.ToList();
        }
        else
        {
            var detailT = scope.GetQualifiedTableName("Group Details", false);
            var sql = $@"SELECT [Code] FROM {detailT} WHERE [Category] = 'CASING PROCUREMENT'";
            var result = await scope.QueryAsync<string>(sql, null, ct);
            return result.ToList();
        }
    }

    /// <summary>
    /// Returns a proc shipment number. If lines already exist for this RC and dispatch date with a
    /// dispatch order that is still entirely Posted (not Dispatched), reuses that number; otherwise
    /// calls NAV NewProcShipNo (same behaviour as Live: allocation is ultimately from NAV).
    /// </summary>
    public async Task<string> NewProcShipNoAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var soap = new SoapFetchParams { RespCenters = param.RespCenters, From = param.From };
        if (!DateTime.TryParse(param.From, out var shipDate))
            shipDate = DateTime.Today;

        var rc = param.RespCenters?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(rc))
        {
            var existing = await TryFindReusableProcShipNoAsync(scope, rc, shipDate, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;
        }

        return await _connector.NewProcShipNoAsync(soap).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds a dispatch order number that already has lines on the given date for this RC where every
    /// line is still Order Status Posted (1), so the shipment batch is not yet dispatched.
    /// </summary>
    private static async Task<string?> TryFindReusableProcShipNoAsync(
        ITenantScope scope, string respCenter, DateTime shipDate, CancellationToken ct)
    {
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var sql = $@"
SELECT TOP 1 Agg.[Dispatch Order No_]
FROM (
    SELECT Line.[Dispatch Order No_],
           SUM(CASE WHEN Line.[Order Status] <> 1 THEN 1 ELSE 0 END) AS NotPostedCount
    FROM {lineT} AS Line
    WHERE Line.[Document Type] = 6
      AND Line.[Responsibility Center] = @rc
      AND CAST(Line.[Dispatch Date] AS DATE) = @date
      AND LTRIM(RTRIM(ISNULL(Line.[Dispatch Order No_], ''))) <> ''
    GROUP BY Line.[Dispatch Order No_]
) AS Agg
WHERE Agg.NotPostedCount = 0
ORDER BY Agg.[Dispatch Order No_] DESC";

        var rows = await scope.QueryAsync<string>(sql, new { rc = respCenter, date = shipDate.Date }, ct).ConfigureAwait(false);
        return rows.FirstOrDefault();
    }

    public async Task<int> UpdateProcurementOrderAsync(ITenantScope scope, OrderInfo order, CancellationToken ct = default)
    {
        return await _connector.UpdateProcurementOrderAsync(new SoapOrderInfo { OrderNo = order.OrderNo, SupplierCode = order.SupplierCode, ManagerCode = order.ManagerCode, Status = order.Status });
    }

    /// <summary>
    /// Ported from Live <c>Tyresoles.One.Data.Navision.Db.Production.GenerateGRAs</c> (<c>Db.Production.cs</c>):
    /// calls NAV <c>GenerateGRAs</c> with the dispatch order / report name; returns comma-separated GRA numbers.
    /// </summary>
    /// <remarks><paramref name="scope"/> is unused (NAV SOAP only, same as Live).</remarks>
    public async Task<string> GenerateGRAsAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        _ = scope;
        return await _connector.GenerateGRAsAsync(param.ReportName).ConfigureAwait(false);
    }

    public async Task<int> InsertProcurementOrderLineAsync(ITenantScope scope, OrderLine order, CancellationToken ct = default)
    {
        return await _connector.InsertProcurementOrderLineAsync(new SoapOrderLine { No = order.No, VendorNo = order.VendorNo, ItemNo = order.ItemNo, Make = order.Make, SerialNo = order.SerialNo, Inspection = order.Inspection, Amount = order.Amount, InspectorCode = order.InspectorCode });
    }

    public async Task<int> UpdateProcurementOrderLineAsync(ITenantScope scope, OrderLine order, CancellationToken ct = default)
    {
        return await _connector.UpdateProcurementOrderLineAsync(new SoapOrderLine { LineNo = order.LineNo, No = order.No, VendorNo = order.VendorNo, ItemNo = order.ItemNo, Make = order.Make, SerialNo = order.SerialNo, Inspection = order.Inspection, Amount = order.Amount, InspectorCode = order.InspectorCode, SubMake = order.SubMake });
    }

    public async Task<int> UpdateProcOrdLineDispatchAsync(ITenantScope scope, List<OrderLineDispatch> lines, CancellationToken ct = default)
    {
        if (lines == null || !lines.Any()) return 0;
        if (lines.Any(c => string.IsNullOrEmpty(c.DispatchOrderNo)))
            throw new Exception("There is atleast one line which doesn't have shipment number.");

        if (await CheckProcShipNoAsync(scope, lines[0].DispatchOrderNo, lines[0].DispatchVehicleNo, lines[0].DispatchMobileNo, lines[0].DispatchTransporter, ct))
            throw new Exception($"Document No {lines[0].DispatchOrderNo} already exists.");

        foreach (var line in lines)
        {
            var result = await _connector.DispatchOrderLineAsync(new SoapOrderLineDispatch
            {
                OrderNo = line.OrderNo, LineNo = line.LineNo, DispatchOrderNo = line.DispatchOrderNo, DispatchDate = line.DispatchDate, 
                DispatchDestination = line.DispatchDestination, DispatchVehicleNo = line.DispatchVehicleNo, 
                DispatchMobileNo = line.DispatchMobileNo, DispatchTransporter = line.DispatchTransporter
            });
            if (result == 0) throw new Exception($"Cannot update {line.No} {line.SerialNo} {line.Make}");
        }
        return 0;
    }

    public async Task<int> UpdateProcOrdLineDispatchSingleAsync(ITenantScope scope, OrderLineDispatch line, CancellationToken ct = default)
    {
        var result = await _connector.UpdateProcurementOrderLine2Async(new SoapOrderLineDispatch
        {
            LineNo = line.LineNo, OrderNo = line.OrderNo, No = line.No, Make = line.Make, SerialNo = line.SerialNo, 
            FactInspection = line.FactInspection, NewSerialNo = line.NewSerialNo, Button = line.Button, Model = line.Model, 
            FactInspector = line.FactInspector, FactInspectorFinal = line.FactInspectorFinal, 
            RejectionReason = line.RejectionReason, Remark = line.Remark, OrderStatus = line.OrderStatus
        });
        if (result == 0) throw new Exception($"Cannot update {line.No} {line.SerialNo} {line.Make}");
        return 0;
    }

    /// <summary>
    /// Ported from Live <c>Tyresoles.One.Data.Navision.Db.Production.UpdateProcOrdLineReceipt</c> (<c>Db.Production.cs</c>):
    /// for each line, calls NAV <c>ReceiptOrderLine</c> (receive at factory). Throws if any SOAP call returns failure.
    /// </summary>
    /// <remarks><paramref name="scope"/> is unused (NAV SOAP only, same as Live).</remarks>
    public async Task<int> UpdateProcOrdLineReceiptAsync(ITenantScope scope, List<OrderLineDispatch> lines, CancellationToken ct = default)
    {
        _ = scope;
        if (lines == null || !lines.Any()) return 0;
        foreach (var line in lines)
        {
            var result = await _connector.ReceiptOrderLineAsync(new SoapOrderLineDispatch { OrderNo = line.OrderNo, LineNo = line.LineNo, Inspector = line.Inspector }).ConfigureAwait(false);
            if (result == 0) throw new Exception($"Cannot update {line.No} {line.SerialNo} {line.Make}");
        }
        return 0;
    }

    /// <summary>
    /// Ported from Live <c>Tyresoles.One.Data.Navision.Db.Production.UpdateProcOrdLineRemove</c> (<c>Db.Production.cs</c>):
    /// for each line, calls NAV <c>RemoveShippedOrdLine</c> (remove from dispatched shipment). Throws if any call fails.
    /// </summary>
    /// <remarks><paramref name="scope"/> is unused (NAV SOAP only, same as Live).</remarks>
    public async Task<int> UpdateProcOrdLineRemoveAsync(ITenantScope scope, List<OrderLineDispatch> lines, CancellationToken ct = default)
    {
        _ = scope;
        if (lines == null || !lines.Any()) return 0;
        foreach (var line in lines)
        {
            var result = await _connector.RemoveShippedOrdLineAsync(new SoapOrderLineDispatch { OrderNo = line.OrderNo, LineNo = line.LineNo }).ConfigureAwait(false);
            if (result == 0) throw new Exception($"Cannot remove {line.No} {line.SerialNo} {line.Make}");
        }
        return 0;
    }

    /// <summary>
    /// Ported from Live <c>Tyresoles.One.Data.Navision.Db.Production.UpdateProcOrdLineDrop</c> (<c>Db.Production.cs</c>):
    /// for each line, calls NAV <c>DropPostedOrdLine</c> (drop posted casing line). Throws if any call fails.
    /// </summary>
    /// <remarks><paramref name="scope"/> is unused (NAV SOAP only, same as Live).</remarks>
    public async Task<int> UpdateProcOrdLineDropAsync(ITenantScope scope, List<OrderLineDispatch> lines, CancellationToken ct = default)
    {
        _ = scope;
        if (lines == null || !lines.Any()) return 0;
        foreach (var line in lines)
        {
            var result = await _connector.DropPostedOrdLineAsync(new SoapOrderLineDispatch { OrderNo = line.OrderNo, LineNo = line.LineNo }).ConfigureAwait(false);
            if (result == 0) throw new Exception($"Cannot remove {line.No} {line.SerialNo} {line.Make}");
        }
        return 0;
    }

    public async Task<int> DeleteProcurementOrderLineAsync(ITenantScope scope, OrderLine order, CancellationToken ct = default)
    {
        return await _connector.DeleteProcurementOrderLineAsync(new SoapOrderLine { No = order.No, LineNo = order.LineNo });
    }

    public async Task<int> DeleteProcurementOrderAsync(ITenantScope scope, OrderInfo order, CancellationToken ct = default)
    {
        return await _connector.DeleteProcurementOrderAsync(new SoapOrderInfo { OrderNo = order.OrderNo });
    }

    public async Task<List<Tile>> GetEcomileProcurementTilesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var tiles = new List<Tile>();
        var openOrders = await GetProcurementOrdersInfoAsync(scope, param, ct);
        tiles.Add(new Tile { Description = "From open orders", Label = "Tyres Booking", Value = openOrders.Sum(c => c.Qty) });

        var postedParams = new FetchParams
        {
            RespCenters = param.RespCenters, View = "Posted", UserCode = param.UserCode,
            UserDepartment = param.UserDepartment, UserType = param.UserType, UserSpecialToken = param.UserSpecialToken
        };
        var postedOrders = await GetProcurementOrdersInfoAsync(scope, postedParams, ct);
        tiles.Add(new Tile { Description = "From posted orders", Label = "Tyres Booked", Value = postedOrders.Sum(c => c.Qty) });

        if (param.UserDepartment != "Production")
        {
            var vendorParams = new FetchParams
            {
                RespCenters = param.RespCenters, UserCode = param.UserCode, UserDepartment = param.UserDepartment,
                UserType = param.UserType, UserSpecialToken = param.UserSpecialToken, Regions = new List<string> { "CASING PROCUREMENT" }
            };
            var vendors = await GetVendorsAsync(scope, vendorParams, ct);
            tiles.Add(new Tile { Description = "Casing Vendors", Label = "Suppliers", Value = vendors.Count });
        }
        return tiles;
    }

    /// <summary>
    /// Ported from Live <c>Tyresoles.One.Data.Navision.Db.Production.ShipmentOrderForMerger</c> (<c>Db.Production.cs</c>):
    /// distinct dispatch shipments from lines with <c>Order Status</c> Dispatched (2) in the last 5 days,
    /// grouped by dispatch order and vehicle details. ECOMGR users restrict rows via vendor <c>Ecomile Proc_ Mgr</c>.
    /// <c>Document Type</c> 6 matches other casing procurement queries on <c>Purchase Line</c>.
    /// </summary>
    public async Task<List<ShipmentInfo>> GetShipmentOrderForMergerAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default)
    {
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);

        var whereUser = "";
        if (string.Equals(param.UserSpecialToken, "ECOMGR", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(param.UserCode))
        {
            whereUser = $" AND Line.[Buy-from Vendor No_] IN (SELECT [No_] FROM {vendorT} WHERE [Ecomile Proc_ Mgr] = @userCode)";
        }

        var sql = $@"
SELECT 
    Line.[Dispatch Order No_] AS [No], Line.[Dispatch Destination] AS [Destination],
    Line.[Dispatch Mobile No] AS [MobileNo], Line.[Dispatch Transporter] AS [Transport],
    Line.[Dispatch Vehicle No_] AS [VehicleNo],
    FORMAT(Line.[Dispatch Date], 'dd-MMM-yy') AS [Date]
FROM {lineT} AS Line
WHERE Line.[Document Type] = 6
  AND Line.[Order Status] = 2
  AND CAST(Line.[Dispatch Date] AS DATE) >= CAST(DATEADD(DAY, -5, GETDATE()) AS DATE)
  {whereUser}
GROUP BY Line.[Dispatch Order No_], Line.[Dispatch Destination], Line.[Dispatch Date], Line.[Dispatch Mobile No], Line.[Dispatch Transporter], Line.[Dispatch Vehicle No_]
ORDER BY Line.[Dispatch Date] DESC";

        var result = await scope.QueryAsync<ShipmentInfo>(sql, new { userCode = param.UserCode }, ct);
        return result.ToList();
    }

    private async Task<bool> CheckProcShipNoAsync(ITenantScope scope, string shipNo, string vehicleNo, string mobileNo, string transport, CancellationToken ct)
    {
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var sql = $@"
SELECT COUNT([Document No_]) 
FROM {lineT} 
WHERE [Dispatch Order No_] = @shipNo AND ([Dispatch Vehicle No_] <> @vehicleNo OR [Dispatch Mobile No] <> @mobileNo OR [Dispatch Transporter] <> @transport)";
        var count = await scope.ExecuteScalarAsync<int>(sql, new { shipNo, vehicleNo, mobileNo, transport }, ct);
        return count > 0;
    }

    #endregion
}
