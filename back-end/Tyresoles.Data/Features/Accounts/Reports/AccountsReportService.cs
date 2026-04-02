using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dataverse.NavLive;
using Tyresoles.Data.Features.Accounts.Reports.Models;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Reporting.Abstractions;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Accounts.Reports;

/// <summary>
/// Accounts report service.
/// Provides GST Reports and E Invoice Errors.
/// </summary>
public sealed class AccountsReportService : IAccountsReportService
{
    private readonly IReportRenderer _reportRenderer;

    private static readonly IReadOnlyList<string> ReportNamesList = new[]
    {
        "GST Report 01", "GST Report 02", "GST Report 03", 
        "GST Report 04", "GST Report 05", "GST Report 06", 
        "E Invoice Errors"
    };

    // Mirrors the legacy AccountsController.ReportMeta output, but is ultimately backed by GroupDetails from tyresoles.sql.
    private static readonly IReadOnlyList<ReportMeta> ReportMetaList = new List<ReportMeta>
    {
        new() { Id = 1, Name = "GST Report 01", ShowType = true, TypeOptions = new List<string> { "Invoice", "Credit Memo" }, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 2, Name = "GST Report 02", ShowType = true, TypeOptions = new List<string> { "Invoice", "Credit Memo" }, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 3, Name = "GST Report 03", ShowType = true, TypeOptions = new List<string> { "Invoice", "Credit Memo" }, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 4, Name = "GST Report 04", ShowType = true, TypeOptions = new List<string> { "Invoice", "Credit Memo" }, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 5, Name = "GST Report 05", ShowType = true, TypeOptions = new List<string> { "Invoice", "Credit Memo" }, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 6, Name = "GST Report 06", ShowType = true, TypeOptions = new List<string> { "Invoice", "Credit Memo" }, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 7, Name = "E Invoice Errors", ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
    };

    public AccountsReportService(IReportRenderer reportRenderer)
    {
        _reportRenderer = reportRenderer ?? throw new ArgumentNullException(nameof(reportRenderer));
    }

    public IReadOnlyList<string> GetReportNames() => ReportNamesList;

    public async Task<List<ReportMeta>> GetReportMetaAsync(ITenantScope scope, string? userId = null, CancellationToken cancellationToken = default)
    {
        // `userId` is accepted for backward compatibility with the legacy endpoint.
        // The current tyresoles.sql GroupDetails mapping does not appear to require it for report metadata.
        _ = userId;

        // Try to retrieve report metadata from tyresoles.sql via GroupDetails (same strategy as SalesReportService).
        // Category value can vary across deployments, so we try a couple of candidates and fall back to static mapping.
        var nameTemplates = ReportMetaList.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var knownNames = new HashSet<string>(ReportMetaList.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);

        var categoriesToTry = new[]
        {
            "RPT-ACCOUNTS",
            "RPT-ACCOUNT",
            "RPT-GST",
            "RPT-ACCT"
        };

        Dataverse.NavLive.GroupDetails[]? detailsArr = null;
        foreach (var category in categoriesToTry)
        {
            var query = scope.Query<GroupDetails>().Where(x => x.Category == category);
            detailsArr = await scope.ToArrayAsync(query.OrderBy(x => x.Name), cancellationToken).ConfigureAwait(false);
            if (detailsArr != null && detailsArr.Length > 0)
                break;
        }

        if (detailsArr == null || detailsArr.Length == 0)
        {
            // Fallback: return the legacy hardcoded meta (ensures endpoint stays functional even if GroupDetails is empty).
            return ReportMetaList
                .Select(m => new ReportMeta
                {
                    Id = m.Id,
                    Code = string.Empty,
                    Name = m.Name,
                    DatePreset = m.DatePreset,
                    OutputFormats = m.OutputFormats,
                    TypeOptions = new List<string>(m.TypeOptions),
                    ViewOptions = new List<string>(m.ViewOptions),
                    ShowType = m.ShowType,
                    ShowView = m.ShowView,
                    ShowCustomers = m.ShowCustomers,
                    ShowDealers = m.ShowDealers,
                    ShowAreas = m.ShowAreas,
                    ShowRegions = m.ShowRegions,
                    ShowRespCenters = m.ShowRespCenters,
                    ShowNos = m.ShowNos,
                    RequiredFields = m.RequiredFields
                })
                .ToList();
        }

        // Filter by report names in memory (query layer may not support names.Contains everywhere).
        var filtered = detailsArr
            .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Name) && knownNames.Contains(d.Name))
            .ToArray();

        var result = new List<ReportMeta>(filtered.Length);
        foreach (var detail in filtered)
        {
            if (nameTemplates.TryGetValue(detail.Name, out var template))
            {
                result.Add(new ReportMeta
                {
                    Id = template.Id,
                    Code = detail.Code,
                    Name = template.Name,
                    DatePreset = template.DatePreset,
                    OutputFormats = template.OutputFormats,
                    TypeOptions = template.TypeOptions,
                    ViewOptions = template.ViewOptions,
                    ShowType = template.ShowType,
                    ShowView = template.ShowView,
                    ShowCustomers = template.ShowCustomers,
                    ShowDealers = template.ShowDealers,
                    ShowAreas = template.ShowAreas,
                    ShowRegions = template.ShowRegions,
                    ShowRespCenters = template.ShowRespCenters,
                    ShowNos = template.ShowNos,
                    RequiredFields = template.RequiredFields
                });
            }
            else
            {
                // Fallback for reports not in hardcoded template list.
                result.Add(new ReportMeta
                {
                    Id = 0,
                    Code = detail.Code,
                    Name = detail.Name,
                    DatePreset = "Today"
                });
            }
        }

        return result.OrderBy(r => r.Id).ThenBy(r => r.Name).ToList();
    }

    public async Task<byte[]> RenderReportAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportName))
            throw new ArgumentException("Report name is required.", nameof(reportName));
        ArgumentNullException.ThrowIfNull(parameters);

        var (rdlcName, dataSource, reportParameters) = await GetReportDataAsync(scope, reportName, parameters, cancellationToken).ConfigureAwait(false);
        if (dataSource == null)
            return Array.Empty<byte>();

        var input = new ReportInput
        {
            Parameters = reportParameters ?? BuildParameters(parameters),
            DataSources = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["DataSet_Result"] = dataSource }
        };

        var isExcel = string.Equals(parameters.ReportOutput, "EXCEL", StringComparison.OrdinalIgnoreCase);
        await using var stream = isExcel
            ? await _reportRenderer.RenderExcelAsync(rdlcName, input, cancellationToken).ConfigureAwait(false)
            : await _reportRenderer.RenderPdfAsync(rdlcName, input, cancellationToken).ConfigureAwait(false);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    private static Dictionary<string, object?>? BuildParameters(SalesReportParams p)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(p.From)) dict["From"] = p.From;
        if (!string.IsNullOrEmpty(p.To)) dict["To"] = p.To;
        return dict.Count > 0 ? dict : null;
    }

    private async Task<(string rdlcName, object? data, Dictionary<string, object?>? reportParameters)> GetReportDataAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams p,
        CancellationToken ct)
    {
        var name = reportName.Trim();
        (string rdlc, object? data) result = name switch
        {
            "GST Report 01" => ("ReportGST01", await GetReportGST01Async(scope, p, ct).ConfigureAwait(false)),
            "GST Report 02" => ("ReportGST02", await GetReportGST02Async(scope, p, ct).ConfigureAwait(false)),
            "GST Report 03" => ("ReportGST03", await GetReportGST03Async(scope, p, ct).ConfigureAwait(false)),
            "GST Report 04" => ("ReportGST04", await GetReportGST04Async(scope, p, ct).ConfigureAwait(false)),
            "GST Report 05" => ("ReportGST05", await GetReportGST05Async(scope, p, ct).ConfigureAwait(false)),
            "GST Report 06" => ("ReportGST06", await GetReportGST06Async(scope, p, ct).ConfigureAwait(false)),
            "E Invoice Errors" => ("ReportEInvoiceErrors", await GetReportEInvoiceErrorsAsync(scope, p, ct).ConfigureAwait(false)),
            _ => ("", null)
        };
        return (result.rdlc, result.data, null);
    }

    private async Task<object?> GetReportGST01Async(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var headerT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Sales Invoice Header", false) : scope.GetQualifiedTableName("Sales Cr_Memo Header", false);
        var lineT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Sales Invoice Line", false) : scope.GetQualifiedTableName("Sales Cr_Memo Header", false);
        var respCenterT = scope.GetQualifiedTableName("Responsibility Center", false);
        var stateT = scope.GetQualifiedTableName("State", true); // State is shared

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Header.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var parameters = new Dictionary<string, object?>();
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }
        parameters["from"] = p.From;
        parameters["to"] = p.To;

        var sql = $@"
SELECT State.[State Code (GST Reg_ No_)] + '-' + State.Description as [State],
    SUM(Line.[Line Amount]) as TaxableAmt,
    ROUND(Line.[GST _], 0, 0) as [TaxPerCent],
    SUM(Line.[Total GST Amount]) as TaxAmt,
    IIF(Line.[GST Jurisdiction Type] = 1, SUM(Line.[Total GST Amount]), 0) as IGST,
    IIF(Line.[GST Jurisdiction Type] = 0, SUM(Line.[Total GST Amount]) / 2, 0) as SGST,
    IIF(Line.[GST Jurisdiction Type] = 0, SUM(Line.[Total GST Amount]) / 2, 0) as CGST,
    IIF(Header.[GST Customer Type] = 1, 'REG', 'URD') as [Type],
    'Report No. 1 (Sales ' + @typeText + ')' as ReportName,
    'Period : ' + @from + ' .. ' + @to as PeriodText,
    RespCenter.[Name] as Location
FROM {headerT} as Header
LEFT JOIN {lineT} as Line ON Line.[Document No_] = Header.[No_]
LEFT JOIN {stateT} as State ON State.[Code] = Header.[GST Bill-to State Code]
LEFT JOIN {respCenterT} as RespCenter ON RespCenter.[Code] = Header.[Responsibility Center]
WHERE Line.[No_] <> '9400' AND Line.[GST Base Amount] <> 0
    AND {respCentersIn}
    AND Header.[Posting Date] BETWEEN @from AND @to
GROUP BY Header.[GST Bill-to State Code], State.[State Code (GST Reg_ No_)], State.[Description], RespCenter.[Name], Line.[GST Jurisdiction Type], Header.[GST Customer Type], ROUND(Line.[GST _], 0, 0)
ORDER BY Header.[GST Bill-to State Code]";

        parameters["typeText"] = p.Type ?? "Invoice";
        
        var results = await scope.RawQueryToArrayAsync<ReportGST01>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetReportGST02Async(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var headerT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Purchase Header", false) : scope.GetQualifiedTableName("Purchase Header", false);
        // Purchase doesn't distinguish Invoice/Credit Memo as cleanly in tables as Sales does, legacy used PurchaseInvoiceHeader or PurchaseCrMemoHeader.
        // Actually, it uses PurchaseInvoiceHeader/PurchaseCrMemoHeader in legacy too.
        headerT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Purchase Invoice Header", false) : scope.GetQualifiedTableName("Purchase Cr_Memo Header", false);
        var lineT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Purchase Invoice Line", false) : scope.GetQualifiedTableName("Purchase Cr_Memo Line", false);
        var respCenterT = scope.GetQualifiedTableName("Responsibility Center", false);

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Header.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var parameters = new Dictionary<string, object?>();
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }
        parameters["from"] = p.From;
        parameters["to"] = p.To;

        var sql = $@"
SELECT Header.No_ as DocumentNo, Line.No_ as Item, Line.Description,
    Header.[Buy-from Vendor Name] as VendorName, Line.[GST Jurisdiction Type] as Interstate,
    SUM(Line.[Line Amount]) as TaxableAmt,
    ROUND(Line.[GST _], 0, 0) as [TaxPerCent],
    SUM(Line.[Total GST Amount]) as TaxAmt,
    'Report No. 1 (' + @typeText + ')' as ReportName,
    'Period : ' + @from + ' .. ' + @to as PeriodText,
    RespCenter.[Name] as Location
FROM {headerT} as Header
LEFT JOIN {lineT} as Line ON Line.[Document No_] = Header.[No_]
LEFT JOIN {respCenterT} as RespCenter ON RespCenter.[Code] = Header.[Responsibility Center]
WHERE Line.No_ IN ('DIESEL', 'PETROL', 'SALT')
    AND {respCentersIn}
    AND Line.[GST _] = 0
    AND Header.[Posting Date] BETWEEN @from AND @to
GROUP BY Line.[GST Jurisdiction Type], Line.[GST _], Header.No_, Header.[Buy-from Vendor Name], Line.Description, Line.No_, RespCenter.[Name]";

        parameters["typeText"] = p.Type ?? "Invoice";
        
        var results = await scope.RawQueryToArrayAsync<ReportGST02>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetReportGST03Async(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var headerT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Sales Invoice Header", false) : scope.GetQualifiedTableName("Sales Cr_Memo Header", false);
        var lineT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Sales Invoice Line", false) : scope.GetQualifiedTableName("Sales Cr_Memo Line", false);
        var customerT = scope.GetQualifiedTableName("Customer", false);
        var stateT = scope.GetQualifiedTableName("State", true);
        var respCenterT = scope.GetQualifiedTableName("Responsibility Center", false);

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Header.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var parameters = new Dictionary<string, object?>();
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }
        parameters["from"] = p.From;
        parameters["to"] = p.To;

        var sqlBase = $@"
SELECT Cust.[Name], Header.[No_] as DocumentNo, Header.[GST Registration No_] as GSTNo,
    FORMAT(Header.[Posting Date], 'dd-MMM-yy') as [Date],
    SUM(Line.[GST Base Amount]) as TaxableAmt,
    SUM(Line.[Total GST Amount]) as TaxAmt,
    SUM(Line.[Amount To Customer]) as TotalAmt,
    IIF(Header.[GST Ship-to State Code] <> '', State2.[Code], State.[Code]) as [State],
    IIF(SUM(Line.[GST Base Amount]) <> 0, ROUND((SUM(Line.[Total GST Amount]) / SUM(Line.[GST Base Amount]) * 100), 0), 0) as TaxPerCent,
    IIF(Line.[GST Jurisdiction Type] = 1, SUM(Line.[Total GST Amount]), 0) as IGST,
    IIF(Line.[GST Jurisdiction Type] = 0, SUM(Line.[Total GST Amount]) / 2, 0) as SGST,
    IIF(Line.[GST Jurisdiction Type] = 0, SUM(Line.[Total GST Amount]) / 2, 0) as CGST,
    'Report No. 3 (Sales ' + @typeText + ')' as ReportName,
    'Period : ' + @from + ' .. ' + @to as PeriodText,
    IIF(@typeText = 'Invoice', 0, 1) as IsCreditMemo,
    RespCenter.[Name] as Location";

        if (p.Type != "Invoice")
        {
            sqlBase += @", Header.[Applies-to Doc_ No_] as ApplyDocNo, 
    CASE Header.[GST Customer Type] WHEN 1 THEN 'R' WHEN 2 THEN 'U' END as GstType";
        }

        var sql = sqlBase + $@"
FROM {headerT} as Header
LEFT JOIN {lineT} as Line ON Line.[Document No_] = Header.[No_]
LEFT JOIN {customerT} as Cust ON Cust.No_ = Header.[Sell-to Customer No_]
LEFT JOIN {stateT} as State ON State.Code = Header.[GST Bill-to State Code]
LEFT JOIN {stateT} as State2 ON State2.Code = Header.[GST Ship-to State Code]
LEFT JOIN {respCenterT} as RespCenter ON RespCenter.Code = Header.[Responsibility Center]
WHERE Header.[GST Customer Type] IN (1, 5)
    AND Header.[Posting Date] BETWEEN @from AND @to
    {respCentersIn}
GROUP BY Header.[No_], Cust.[Name], Header.[Posting Date], RespCenter.[Name], Header.[GST Registration No_], Header.[GST Ship-to State Code], Header.[GST Bill-to State Code], State2.[Code], State.[Code], Line.[GST Jurisdiction Type]";

        if (p.Type != "Invoice")
        {
            sql += ", Header.[Applies-to Doc_ No_], Header.[GST Customer Type]";
        }
        
        sql += " ORDER BY Header.[No_]";

        parameters["typeText"] = p.Type ?? "Invoice";
        
        var results = await scope.RawQueryToArrayAsync<ReportGST03>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetReportGST04Async(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var headerT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Sales Invoice Header", false) : scope.GetQualifiedTableName("Sales Cr_Memo Header", false);
        var lineT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Sales Invoice Line", false) : scope.GetQualifiedTableName("Sales Cr_Memo Line", false);
        var custT = scope.GetQualifiedTableName("Customer", false);
        var stateT = scope.GetQualifiedTableName("State", true);
        var respCenterT = scope.GetQualifiedTableName("Responsibility Center", false);

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Header.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var parameters = new Dictionary<string, object?>();
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }
        parameters["from"] = p.From;
        parameters["to"] = p.To;

        var sql = $@"
SELECT SUM(Line.[GST Base Amount]) as TaxableAmt,
    SUM(Line.[Total GST Amount]) as TaxAmt,
    IIF(Line.[GST Jurisdiction Type] = 1, SUM(Line.[Total GST Amount]), 0) as IGST,
    IIF(Line.[GST Jurisdiction Type] = 0, SUM(Line.[Total GST Amount]) / 2, 0) as SGST,
    IIF(Line.[GST Jurisdiction Type] = 0, SUM(Line.[Total GST Amount]) / 2, 0) as CGST,
    IIF(Line.[GST Jurisdiction Type] = 1, 'Interstate', 'Intrastate') as SupplyType,
    IIF(Header.[GST Ship-to State Code] <> '', State2.[State Code (GST Reg_ No_)] + '-' + State2.Description, State.[State Code (GST Reg_ No_)] + '-' + State.Description) as State,
    IIF(SUM(Line.[GST Base Amount]) <> 0, ROUND((SUM(Line.[Total GST Amount]) / SUM(Line.[GST Base Amount]) * 100), 0), 0) as TaxPerCent,
    'Report No. 4 (Sales ' + @typeText + ')' as ReportName,
    'Period : ' + @from + ' .. ' + @to as PeriodText,
    RespCenter.[Name] as Location
FROM {headerT} as Header
LEFT JOIN {lineT} as Line ON Line.[Document No_] = Header.[No_]
LEFT JOIN {custT} as Cust ON Cust.No_ = Header.[Sell-to Customer No_]
LEFT JOIN {stateT} as State ON State.Code = Header.[GST Bill-to State Code]
LEFT JOIN {stateT} as State2 ON State2.Code = Header.[GST Ship-to State Code]
LEFT JOIN {respCenterT} as RespCenter ON RespCenter.Code = Header.[Responsibility Center]
WHERE Header.[GST Customer Type] IN (0, 2)
    AND Header.[Posting Date] BETWEEN @from AND @to
    AND Line.[GST Base Amount] <> 0
    {respCentersIn}
GROUP BY Header.[No_], RespCenter.[Name], Header.[GST Ship-to State Code], Header.[GST Bill-to State Code], Line.[GST Jurisdiction Type], State2.[State Code (GST Reg_ No_)], State.[State Code (GST Reg_ No_)], State2.Description, State.Description
ORDER BY Header.[No_]";

        parameters["typeText"] = p.Type ?? "Invoice";

        var results = await scope.RawQueryToArrayAsync<ReportGST04>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetReportGST05Async(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var lineT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Sales Invoice Line", false) : scope.GetQualifiedTableName("Sales Cr_Memo Line", false);
        var hsnT = scope.GetQualifiedTableName("HSN_SAC", false);
        var respCenterT = scope.GetQualifiedTableName("Responsibility Center", false);

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Line.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var parameters = new Dictionary<string, object?>();
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }
        parameters["from"] = p.From;
        parameters["to"] = p.To;

        var sql = $@"
SELECT HSN.Code as HSN, HSN.Description, Line.[Unit of Measure Code] as UQC, Line.[GST Jurisdiction Type] as IsInterstate,
    RespCenter.[Name] as Location,
    SUM(Line.Quantity) as TotalQuantity,
    SUM(Line.[Amount To Customer]) as TotalValue,
    SUM(Line.[GST Base Amount]) as TotalTaxableValue,
    SUM(Line.[Total GST Amount]) as TaxAmt,
    'Report No. 5 (Sales ' + @typeText + ')' as ReportName,
    'Period : ' + @from + ' .. ' + @to as PeriodText
FROM {lineT} as Line
LEFT JOIN {hsnT} as HSN ON HSN.Code = Line.[HSN_SAC Code] AND HSN.[GST Group Code] = Line.[GST Group Code]
LEFT JOIN {respCenterT} as RespCenter ON RespCenter.[Code] = Line.[Responsibility Center]
WHERE Line.[HSN_SAC Code] <> '' AND Line.[No_] <> '9400' AND Line.[GST Base Amount] <> 0
    AND Line.[Posting Date] BETWEEN @from AND @to
    {respCentersIn}
GROUP BY HSN.[Code], HSN.[Description], Line.[Unit of Measure Code], Line.[GST Jurisdiction Type], RespCenter.[Name]
ORDER BY HSN.[Code]";

        parameters["typeText"] = p.Type ?? "Invoice";

        var rawList = await scope.RawQueryToArrayAsync<ReportGST05>(sql, parameters, ct).ConfigureAwait(false);
        
        // Mem-grouping as per legacy logic
        var records = new List<ReportGST05>();
        if (rawList != null && rawList.Length > 0)
        {
            var hsnGroups = rawList.GroupBy(r => r.HSN);
            foreach (var hsnGroup in hsnGroups)
            {
                var uomGroups = hsnGroup.GroupBy(r => r.UQC);
                foreach (var uomGroup in uomGroups)
                {
                    var record = new ReportGST05
                    {
                        HSN = hsnGroup.Key,
                        UQC = uomGroup.Key
                    };
                    bool first = true;
                    foreach (var rec in uomGroup)
                    {
                        if (first)
                        {
                            record.ReportName = rec.ReportName;
                            record.PeriodText = rec.PeriodText;
                            record.Location = rec.Location;
                            record.Description = rec.Description;
                            first = false;
                        }
                        record.TotalQuantity += rec.TotalQuantity;
                        record.TotalValue += rec.TotalValue;
                        record.TotalTaxableValue += rec.TotalTaxableValue;

                        if (rec.IsInterstate)
                            record.IntegratedTax += rec.TaxAmt;
                        else
                        {
                            var taxHalf = rec.TaxAmt > 0 ? rec.TaxAmt / 2 : 0;
                            record.CenteralTax += taxHalf;
                            record.StateUtTax += taxHalf;
                        }
                    }
                    records.Add(record);
                }
            }
        }
        return ToDataTable(records);
    }

    private async Task<object?> GetReportGST06Async(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var headerT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Purchase Invoice Header", false) : scope.GetQualifiedTableName("Purchase Cr_Memo Header", false);
        var lineT = p.Type == "Invoice" ? scope.GetQualifiedTableName("Purchase Invoice Line", false) : scope.GetQualifiedTableName("Purchase Cr_Memo Line", false);
        var respCenterT = scope.GetQualifiedTableName("Responsibility Center", false);
        var stateT = scope.GetQualifiedTableName("State", true);

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Header.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var parameters = new Dictionary<string, object?>();
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }
        parameters["from"] = p.From;
        parameters["to"] = p.To;

        var sql = $@"
SELECT Header.[GST Registration No_] as GSTIN, Header.[Buy-from Vendor Name] as [Name],
    Header.[Vendor Invoice No_] as InvoiceNo, Header.[No_] as DocumentNo, RespCenter.[Name] as Location,
    Header.[State] as PlaceOfSupply,
    FORMAT(Header.[Posting Date], 'dd-MMM-yy') as InvDate,
    SUM(Line.[Amount To Vendor]) as TotalInvoiceValue,
    SUM(Line.[GST Base Amount]) as Taxable,
    'Report No. 6 (Purchase ' + @typeText + ')' as ReportName,
    'Period : ' + @from + ' .. ' + @to as PeriodText,
    IIF(Line.[GST Jurisdiction Type] = 1, SUM(Line.[Total GST Amount]), 0) as IGST,
    IIF(Line.[GST Jurisdiction Type] = 0, SUM(Line.[Total GST Amount]) / 2, 0) as SGST,
    IIF(Line.[GST Jurisdiction Type] = 0, SUM(Line.[Total GST Amount]) / 2, 0) as CGST
FROM {headerT} as Header
LEFT JOIN {lineT} as Line ON Line.[Document No_] = Header.[No_]
LEFT JOIN {respCenterT} as RespCenter ON RespCenter.[Code] = Header.[Responsibility Center]
WHERE Header.[GST Vendor Type] = 1
    AND Header.[Posting Date] BETWEEN @from AND @to
    {respCentersIn}
GROUP BY Header.[GST Registration No_], Header.[No_], Header.[Buy-from Vendor Name], Header.[Vendor Invoice No_], RespCenter.[Name], Header.[State], Line.[GST Jurisdiction Type], Header.[Posting Date]
ORDER BY Header.[No_]";

        parameters["typeText"] = p.Type ?? "Invoice";

        var results = await scope.RawQueryToArrayAsync<ReportGST06>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetReportEInvoiceErrorsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var logT = scope.GetQualifiedTableName("GST Api Log", false);
        
        var sql = $@"
SELECT IIF([Document Type] = 1, 'Invoice', 'Cred Memo') as DocType,
    [Document No_] as DocumentNo,
    [Error Message] as Error,
    [Resp_ Center] as RespCenter,
    FORMAT([Date], 'dd-MMM-yy') as [Date],
    'E Invoice Errors' as ReportName,
    'Period : ' + @from + ' .. ' + @to as PeriodText
FROM {logT}
WHERE [Date] BETWEEN @from AND @to";

        var parameters = new Dictionary<string, object?>
        {
            ["from"] = p.From ?? DateTime.Now.AddMonths(-1).ToString("yyyy-MM-01"),
            ["to"] = p.To ?? DateTime.Now.ToString("yyyy-MM-dd")
        };

        var results = await scope.RawQueryToArrayAsync<EInvoiceErrors>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private static readonly ConcurrentDictionary<Type, (PropertyInfo[] Props, Type[] ColTypes)> ToDataTableSchemaCache = new();

    private static DataTable? ToDataTable<T>(IEnumerable<T> rows)
    {
        var type = typeof(T);
        var (props, colTypes) = ToDataTableSchemaCache.GetOrAdd(type, t =>
        {
            var p = t.GetProperties();
            var types = new Type[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                var propType = p[i].PropertyType;
                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    propType = Nullable.GetUnderlyingType(propType)!;
                types[i] = propType == typeof(DateTime) ? typeof(DateTime) : propType == typeof(decimal) ? typeof(decimal) : propType == typeof(int) ? typeof(int) : propType == typeof(bool) ? typeof(bool) : typeof(string);
            }
            return (p, types);
        });

        var dt = new DataTable();
        for (int i = 0; i < props.Length; i++)
            dt.Columns.Add(props[i].Name, colTypes[i]);

        foreach (var r in rows)
        {
            var row = dt.NewRow();
            for (int i = 0; i < props.Length; i++)
            {
                var val = props[i].GetValue(r);
                row[props[i].Name] = val ?? DBNull.Value;
            }
            dt.Rows.Add(row);
        }
        return dt;
    }
}
