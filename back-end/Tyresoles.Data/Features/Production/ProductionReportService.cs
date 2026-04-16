using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dataverse.NavLive;
using Tyresoles.Data.Features.Production.Models;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Data.Features.Sales.Reports.Models;
using Tyresoles.Reporting.Abstractions;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Production;

/// <summary>
/// Production report service.
/// Provides Ecomile Aging, Exchange Tyres, Claim & Failure, Proc/Dispatch Orders, etc.
/// Ported from legacy Navision Providers and Db.Production.
/// </summary>
public sealed class ProductionReportService : IProductionReportService
{
    private readonly IReportRenderer _reportRenderer;

    private static readonly IReadOnlyList<string> ReportNamesList = new[]
    {
        "Ecomile Aging",
        "Exchange Tyres",
        "Claim & Failure",
        "Posted Proc Order",
        "Posted Dispatch Order",
        "Posted Dispatch Details",
        "Casing Inspection",
        "Casing New Numbering",
        "Vendor Bill",
        "Casing Purchase Details",
        "Casing Purchase Analysis",
        "Ecomile Inv. Sales Stat.",
        "Claim Analysis",
        "Casing Average Cost",
        "Claim Ratios",
    };

    // Aligns with legacy ProductionReportData and RDLC requirements.
    private static readonly IReadOnlyList<ReportMeta> ReportMetaList = new List<ReportMeta>
    {
        new() { Id = 1, Name = "Ecomile Aging", ShowType = true, TypeOptions = new List<string> { "Ecomile", "Casing" }, ShowRespCenters = true, DatePreset = "Today", OutputFormats = "pdf,excel", RequiredFields = "respCenters" },
        new() { Id = 2, Name = "Exchange Tyres", ShowRespCenters = true, DatePreset = "Today", OutputFormats = "pdf,excel", RequiredFields = "respCenters" },
        new() { Id = 3, Name = "Claim & Failure", ShowType = true, ShowView = true, TypeOptions = new List<string> { "Claim", "Failure" }, ViewOptions = new List<string> { "Tyre-Vs-Decision", "Fault-Vs-Decision", "Customer-Vs-Decision", "Dealer-Vs-Decision", "Area-Vs-Decision", "Region-Vs-Decision" }, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 4, Name = "Posted Proc Order", ShowNos = true, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "nos" },
        new() { Id = 5, Name = "Posted Dispatch Order", ShowNos = true, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "nos" },
        new() { Id = 6, Name = "Posted Dispatch Details", ShowNos = true, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "nos" },
        new() { Id = 7, Name = "Casing Inspection", ShowNos = true, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "nos" },
        new() { Id = 8, Name = "Casing New Numbering", ShowNos = true, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "nos" },
        new() { Id = 9, Name = "Vendor Bill", ShowType = true, TypeOptions = new List<string> { "Posted", "Dispatch" }, ShowNos = true, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "nos" },
        new() { Id = 10, Name = "Casing Purchase Details", ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 11, Name = "Casing Purchase Analysis", ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 12, Name = "Ecomile Inv. Sales Stat.", ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 13, Name = "Claim Analysis", ShowType = true, ShowView = true, TypeOptions = new List<string> { "Claim Receipt", "Claim Sanctioned" }, ViewOptions = new List<string> { "Customers", "Dealers", "Fault Reasons", "Tread Patterns", "Casing Makes", "Prod. Agings" }, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 14, Name = "Casing Average Cost", ShowNos = true, ShowRespCenters = true, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "nos" },
        new(){ Id = 15, Name="Claim Ratios", ShowView=true, ViewOptions= new List<string>{"Product wise","Pattern wise","Make wise","Submake wise","Dealer wise","Salesperson wise","Defect wise","Proc. Market wise" }, DatePreset="thisMonth, lastMonth",OutputFormats = "pdf,excel", RequiredFields = "dateRange,view,respCenters" }
    };

    public ProductionReportService(IReportRenderer reportRenderer)
    {
        _reportRenderer = reportRenderer ?? throw new ArgumentNullException(nameof(reportRenderer));
    }

    public IReadOnlyList<string> GetReportNames() => ReportNamesList;

    public async Task<List<ReportMeta>> GetReportMetaAsync(ITenantScope scope, string? userId = null, CancellationToken cancellationToken = default)
    {
        // Aligns with the strategy in AccountsReportService and SalesReportService.
        // Tries to fetch from GroupDetails but falls back to static if empty.
        var categoriesToTry = new[] { "RPT-PROD", "RPT-PRODUCTION" };
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
            return ReportMetaList.Select(CopyMeta).ToList();
        }

        var knownNames = new HashSet<string>(ReportMetaList.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
        var templates = ReportMetaList.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

        var result = new List<ReportMeta>();
        foreach (var detail in detailsArr)
        {
            if (detail.Name != null && templates.TryGetValue(detail.Name, out var template))
            {
                var copy = CopyMeta(template);
                copy.Code = detail.Code;
                result.Add(copy);
            }
        }
        return result.OrderBy(r => r.Id).ThenBy(r => r.Name).ToList();
    }

    private static ReportMeta CopyMeta(ReportMeta m) => new()
    {
        Id = m.Id,
        Code = m.Code,
        Name = m.Name,
        DatePreset = m.DatePreset,
        OutputFormats = m.OutputFormats,
        TypeOptions = m.TypeOptions != null ? new List<string>(m.TypeOptions) : null,
        ViewOptions = m.ViewOptions != null ? new List<string>(m.ViewOptions) : null,
        ShowType = m.ShowType,
        ShowView = m.ShowView,
        ShowCustomers = m.ShowCustomers,
        ShowDealers = m.ShowDealers,
        ShowAreas = m.ShowAreas,
        ShowRegions = m.ShowRegions,
        ShowRespCenters = m.ShowRespCenters,
        ShowNos = m.ShowNos,
        RequiredFields = m.RequiredFields
    };

    public async Task<byte[]> RenderReportAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportName))
            throw new ArgumentException("Report name is required.", nameof(reportName));
        ArgumentNullException.ThrowIfNull(parameters);

        var (rdlcName, dataSource, reportParameters) = await GetReportDataSourceAsync(scope, reportName, parameters, cancellationToken).ConfigureAwait(false);
        if (dataSource == null)
            return Array.Empty<byte>();

        var input = new ReportInput
        {
            Parameters = reportParameters ?? BuildDefaultParameters(parameters),
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

    private static Dictionary<string, object?>? BuildDefaultParameters(SalesReportParams p)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(p.From)) dict["From"] = p.From;
        if (!string.IsNullOrEmpty(p.To)) dict["To"] = p.To;
        return dict.Count > 0 ? dict : null;
    }

    private async Task<(string rdlcName, object? data, Dictionary<string, object?>? reportParameters)> GetReportDataSourceAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams p,
        CancellationToken ct)
    {
        var name = reportName.Trim();
        return name switch
        {
            "Ecomile Aging" => ("EcomileAging", await GetEcomileAgingAsync(scope, p, ct).ConfigureAwait(false), null),
            "Exchange Tyres" => ("ExchangeTyres", await GetExchangeTyresAsync(scope, p, ct).ConfigureAwait(false), null),
            "Claim & Failure" => ("ClaimFailure", await GetClaimFailureAsync(scope, p, ct).ConfigureAwait(false), null),
            "Posted Proc Order" => ("PostedProcOrder", await GetPostedProcOrderAsync(scope, p, ct).ConfigureAwait(false), null),
            "Posted Dispatch Order" => ("PostedDispatchOrder", await GetPostedProcOrderAsync(scope, p, ct).ConfigureAwait(false), null),
            "Posted Dispatch Details" => ("PostedDispatchDetails", await GetPostedProcOrderAsync(scope, p, ct).ConfigureAwait(false), null),
            "Casing Inspection" => ("CasingInspection", await GetPostedProcOrderAsync(scope, p, ct).ConfigureAwait(false), null),
            "Casing New Numbering" => ("CasingNewNumbering", await GetPostedProcOrderAsync(scope, p, ct).ConfigureAwait(false), null),
            "Vendor Bill" => ("VendorBill", await GetVendorBillAsync(scope, p, ct).ConfigureAwait(false), null),
            "Casing Purchase Details" => ("CasingPurchaseDetails", await GetCasingPurchaseDetailsAsync(scope, p, ct).ConfigureAwait(false), BuildCasingPurchaseReportParameters(p)),
            "Casing Purchase Analysis" => ("CasingPurchaseAnalysis", await GetCasingPurchaseAnalysisAsync(scope, p, ct).ConfigureAwait(false), BuildCasingPurchaseReportParameters(p)),
            "Ecomile Inv. Sales Stat." => ("EcomileInvSalesStatistics", await GetEcomileInvSalesStatisticsAsync(scope, p, ct).ConfigureAwait(false), null),
            "Claim Analysis" => ("ClaimAnalysis", await GetClaimAnalysisAsync(scope, p, ct).ConfigureAwait(false), null),
            "Casing Average Cost" => ("PostedDispatchAverageCost", await GetPostedDispatchAverageCostAsync(scope, p, ct).ConfigureAwait(false), null),
            "Claim Ratios" => ("ClaimRatios", await GetClaimRatiosAsync(scope, p, ct).ConfigureAwait(false), null),
            _ => ("", null, null)
        };
    }

    #region Report Implementations

    /// <summary>
    /// Tyresoles business dates use India (IST). API may send ISO UTC (e.g. 2026-02-28T18:30:00.000Z = 01-Mar-26 00:00 IST).
    /// </summary>
    private static TimeZoneInfo GetIndiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
    }

    private static DateTime? TryParseToIndiaDateOnly(string? value, TimeZoneInfo tz)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            var local = TimeZoneInfo.ConvertTime(dto, tz);
            return local.Date;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
        {
            var utcDto = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero);
            var local = TimeZoneInfo.ConvertTime(utcDto, tz);
            return local.Date;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.Date;

        return null;
    }

    /// <summary>
    /// SQL <c>yyyy-MM-dd</c> bounds and display strings <c>dd-MMM-yy</c> for casing purchase reports.
    /// </summary>
    private static (string FromSql, string ToSql, string DateRangeLabel, string FromDisplay, string ToDisplay) ResolveCasingPurchaseSqlAndDisplayRange(
        SalesReportParams p)
    {
        var tz = GetIndiaTimeZone();
        var nowIndia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;

        var fromDate = new DateTime(nowIndia.Year, nowIndia.Month, 1).AddMonths(-1);
        var toDate = nowIndia;

        if (!string.IsNullOrWhiteSpace(p.From))
        {
            var d = TryParseToIndiaDateOnly(p.From, tz);
            if (d.HasValue)
                fromDate = d.Value;
        }

        if (!string.IsNullOrWhiteSpace(p.To))
        {
            var d = TryParseToIndiaDateOnly(p.To, tz);
            if (d.HasValue)
                toDate = d.Value;
        }

        var inv = CultureInfo.InvariantCulture;
        string Fmt(DateTime d) => d.ToString("dd-MMM-yy", inv);
        var label = $"For period {Fmt(fromDate)} .. {Fmt(toDate)}";

        return (
            fromDate.ToString("yyyy-MM-dd", inv),
            toDate.ToString("yyyy-MM-dd", inv),
            label,
            Fmt(fromDate),
            Fmt(toDate));
    }

    private static Dictionary<string, object?>? BuildCasingPurchaseReportParameters(SalesReportParams p)
    {
        var (_, _, _, fromDisplay, toDisplay) = ResolveCasingPurchaseSqlAndDisplayRange(p);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["From"] = fromDisplay,
            ["To"] = toDisplay,
        };
    }

    private async Task<object?> GetEcomileAgingAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var table = scope.GetQualifiedTableName("Ecomile Items_", false);
        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND [Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var statusFilter = p.Type == "Casing" ? " AND [Status] = 0" : " AND [Status] = 2";
        
        var sql = $@"
SELECT [No_] as No, DATEDIFF(DAY, [Pur_ Date], GETDATE()) as Age
FROM {table}
WHERE [On Exchange] = 0 {respCentersIn} {statusFilter}";

        var parameters = new Dictionary<string, object?>();
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }

        var tyreRecords = await scope.RawQueryToArrayAsync<TyreAgeRow>(sql, parameters, ct).ConfigureAwait(false);
        if (tyreRecords == null || tyreRecords.Length == 0) return null;

        var result = new List<EcomileAgingRow>();
        var sizes = tyreRecords.GroupBy(c => c.No).Select(c => c.First()).ToList();
        var dateRanges = new[] { (0, 29), (30, 59), (60, 89), (90, 179), (180, 99999) };
        var reportName = (p.Type ?? "Ecomile") + " Aging Report";
        var period = $"As of {DateTime.Now:dd. MMMM yyyy}";

        foreach (var size in sizes)
        {
            var row = new EcomileAgingRow { Tyre = size.No, ReportName = reportName, Period = period };
            foreach (var range in dateRanges)
            {
                var count = tyreRecords.Count(c => c.No == size.No && c.Age >= range.Item1 && c.Age <= range.Item2);
                switch (range.Item1)
                {
                    case 0: row.Age30 = count; break;
                    case 30: row.Age60 = count; break;
                    case 60: row.Age90 = count; break;
                    case 90: row.Age180 = count; break;
                    case 180: row.AgeAbove = count; break;
                }
            }
            row.Total = row.Age30 + row.Age60 + row.Age90 + row.Age180 + row.AgeAbove;
            result.Add(row);
        }
        return ToDataTable(result);
    }

    private async Task<object?> GetExchangeTyresAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var ecomileTable = scope.GetQualifiedTableName("Ecomile Items_", false);
        var itemTable = scope.GetQualifiedTableName("Item", false);
        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND E.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";

        var sql = $@"
SELECT E.[No_] as No, E.[Status], I.[Product Group Code] as [Group]
FROM {ecomileTable} E
LEFT JOIN {itemTable} I ON I.[No_] = E.[No_]
WHERE E.[On Exchange] = 1 {respCentersIn}";

        var parameters = new Dictionary<string, object?>();
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }

        var tyreRecords = await scope.RawQueryToArrayAsync<EcomileItemStatusRow>(sql, parameters, ct).ConfigureAwait(false);
        if (tyreRecords == null || tyreRecords.Length == 0) return null;

        var row = new ExchangeTyresRow
        {
            ReportName = "Exchange Tyre",
            Period = $"Till Date {DateTime.Now:dd. MMM yyyy}"
        };

        row.TotG = tyreRecords.Count(c => c.Group == "GIANT");
        row.TotR = tyreRecords.Count(c => c.Group == "RADIAL G");
        row.TotL = tyreRecords.Count(c => c.Group == "LCV");
        row.TotP = tyreRecords.Count(c => c.Group == "PASS");
        row.TotT = tyreRecords.Count(c => c.Group == "TRACTOR");
        row.TotO = tyreRecords.Count(c => c.Group == "OTR");
        row.TotO1 = tyreRecords.Count(c => c.Group == "OTR 1");

        const EcomileStatusRow invoice = EcomileStatusRow.Invoice;
        row.SoldG = tyreRecords.Count(c => c.Group == "GIANT" && c.Status == invoice);
        row.SoldR = tyreRecords.Count(c => c.Group == "RADIAL G" && c.Status == invoice);
        row.SoldL = tyreRecords.Count(c => c.Group == "LCV" && c.Status == invoice);
        row.SoldP = tyreRecords.Count(c => c.Group == "PASS" && c.Status == invoice);
        row.SoldT = tyreRecords.Count(c => c.Group == "TRACTOR" && c.Status == invoice);
        row.SoldO = tyreRecords.Count(c => c.Group == "OTR" && c.Status == invoice);
        row.SoldO1 = tyreRecords.Count(c => c.Group == "OTR 1" && c.Status == invoice);

        row.G = tyreRecords.Count(c => c.Group == "GIANT" && c.Status != invoice);
        row.R = tyreRecords.Count(c => c.Group == "RADIAL G" && c.Status != invoice);
        row.L = tyreRecords.Count(c => c.Group == "LCV" && c.Status != invoice);
        row.P = tyreRecords.Count(c => c.Group == "PASS" && c.Status != invoice);
        row.T = tyreRecords.Count(c => c.Group == "TRACTOR" && c.Status != invoice);
        row.O = tyreRecords.Count(c => c.Group == "OTR" && c.Status != invoice);
        row.O1 = tyreRecords.Count(c => c.Group == "OTR 1" && c.Status != invoice);

        return ToDataTable(new[] { row });
    }

    private async Task<object?> GetClaimRatiosAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var ileT = scope.GetQualifiedTableName("Item Ledger Entry", false);
        var custT = scope.GetQualifiedTableName("Customer", false);
        var locT = scope.GetQualifiedTableName("Location", false);
        var postedT = scope.GetQualifiedTableName("Claim & Failure Posted", false);
        var itemT = scope.GetQualifiedTableName("Item", false);
        var settleT = scope.GetQualifiedTableName("Claim & Failure Settlement", false);
        var glT = scope.GetQualifiedTableName("G_L Entry", false);
        var crHeaderT = scope.GetQualifiedTableName("Sales Cr_Memo Header", false);
        var crLineT = scope.GetQualifiedTableName("Sales Cr_Memo Line", false);

        var dr = ResolveCasingPurchaseSqlAndDisplayRange(p);

        var locations = p.RespCenters?.Count > 0 ? string.Join(", ", p.RespCenters) : "";    
        var respCentersIn = p.RespCenters?.Count > 0 
            ? $" AND Loc.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" 
            : " AND Loc.[Responsibility Center] IN ('AHM', 'BEL', 'JBP')";

        var postedRespCentersIn = p.RespCenters?.Count > 0
            ? $" AND Posted.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})"
            : " AND Posted.[Responsibility Center] IN ('AHM', 'BEL', 'JBP')";

        var glRespCentersIn = p.RespCenters?.Count > 0
            ? $" AND GLEntry.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})"
            : " AND GLEntry.[Responsibility Center] IN ('AHM', 'BEL', 'JBP')";

        var headerRespCentersIn = p.RespCenters?.Count > 0
            ? $" AND Header.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})"
            : " AND Header.[Responsibility Center] IN ('AHM', 'BEL', 'JBP')";

        var particularILE = "ILE.[Product Group Code] as Particular";
        var particularILEGroup = "ILE.[Product Group Code]";
        var particularLbl = "Product";
        var particularClaims = "Item.[Product Group Code] as Particular";
        var particularClaims2 = "Particular";
        var particularClaimsGroup = "Particular";
        var joinILE = "";
        var joinClaims = "";
        bool bValue = false;

        switch (p.View)
        {
            case "Product wise":
                {
                    particularILE = "ILE.[Product Group Code] as Particular";
                    particularILEGroup = "ILE.[Product Group Code]";
                    particularLbl = "Product";
                    particularClaims = "Item.[Product Group Code] as Particular,";
                    particularClaims2 = "Particular,";
                    particularClaimsGroup = "Particular";
                    bValue = true;
                    break;
                }
            case "Pattern wise":
                {
                    particularILE = "IV.Pattern as Particular, ILE.[Product Group Code] as [Group]";
                    particularILEGroup = "IV.Pattern, ILE.[Product Group Code]";
                    joinILE = "LEFT join [Tyresoles (India) Pvt_ Ltd_$Item Variant] as IV on IV.[Item No_] = ILE.[Item No_] and IV.Code = ILE.[Variant Code]";
                    particularLbl = "Pattern";
                    particularClaims = "Item.[Product Group Code] as [Group], InvLines.Pattern as Particular,";
                    particularClaims2 = "Particular, [Group],";
                    joinClaims = "LEFT JOIN [Tyresoles (India) Pvt_ Ltd_$Sales Invoice Line] as InvLines on InvLines.[Document No_] = Posted.[Invoice No_] and InvLines.[Line No_] = Posted.[Line No_] ";
                    particularClaimsGroup = "Particular, [Group]";
                    break;
                }
            case "Make wise":
                {
                    particularILE = "ILE.Make as Particular, ILE.[Product Group Code] as [Group]";
                    particularILEGroup = "ILE.Make, ILE.[Product Group Code]";                    
                    particularLbl = "Make";
                    particularClaims = "Item.[Product Group Code] as [Group], Posted.Make as Particular,";
                    particularClaims2 = "Particular, [Group],";                    
                    particularClaimsGroup = "Particular, [Group]";
                    break;
                }
            case "Submake wise":
                {
                    particularILE = "ILE.[Sub Make] as Particular, Item.[Alternative Item No_]+' '+ILE.Make as [Group]";
                    particularILEGroup = "ILE.[Sub Make], Item.[Alternative Item No_]+' '+ILE.Make";
                    particularLbl = "Sub Make";
                    joinILE = "Left Join [Tyresoles (India) Pvt_ Ltd_$Item] as Item on Item.No_ = ILE.[Item No_] ";
                    particularClaims = "Item.[Alternative Item No_]+' '+Posted.[Make] as [Group], Posted.[Sub Make] as Particular,";
                    particularClaims2 = "Particular, [Group],";
                    particularClaimsGroup = "Particular, [Group]";
                    break;
                }
            case "Dealer wise":
                {
                    particularILE = "Dealer.[Dealership Name] as Particular, ILE.[Product Group Code] as [Group]";
                    particularILEGroup = "Dealer.[Dealership Name], ILE.[Product Group Code]";
                    particularLbl = "Dealer";
                    joinILE = "LEFT JOIN [Tyresoles (India) Pvt_ Ltd_$Salesperson_Purchaser] as Dealer on Dealer.Code = Cust.[Dealer Code]";
                    particularClaims = "Item.[Product Group Code] as [Group], Dealer.[Dealership Name] as Particular,";
                    joinClaims = "Left join [Tyresoles (India) Pvt_ Ltd_$Customer] as Customer on Customer.No_ = Posted.[Customer No_]\r\n  Left join [Tyresoles (India) Pvt_ Ltd_$Salesperson_Purchaser] as Dealer on Dealer.Code = Customer.[Dealer Code]";
                    particularClaims2 = "Particular, [Group],";
                    particularClaimsGroup = "Particular, [Group]";
                    break;
                }
            case "Salesperson wise":
                {
                    particularILE = "ISNULL(Salesperson.Name,'') as Particular, ILE.[Product Group Code] as [Group]";
                    particularILEGroup = "Salesperson.Name, ILE.[Product Group Code]";
                    particularLbl = "Salesperson";
                    joinILE = "LEFT JOIN (select * from [Tyresoles (India) Pvt_ Ltd_$Team Salesperson] where [Type] = 0) as Salesperson on Salesperson.[Team Code] = Cust.[Area Code]";
                    particularClaims = "Item.[Product Group Code] as [Group], ISNULL(Salesperson.Name,'') as Particular,";
                    joinClaims = "Left join [Tyresoles (India) Pvt_ Ltd_$Customer] as Customer on Customer.No_ = Posted.[Customer No_]\r\n  LEFT JOIN (select * from [Tyresoles (India) Pvt_ Ltd_$Team Salesperson] where [Type] = 0) as Salesperson on Salesperson.[Team Code] = Customer.[Area Code]";
                    particularClaims2 = "Particular, [Group],";
                    particularClaimsGroup = "Particular, [Group]";
                    break;
                }
            case "Proc. Market wise":
                {
                    particularILE = "ISNULL(Vendor.[Group Details],'') as Particular, ILE.[Product Group Code] as [Group]";
                    particularILEGroup = "Vendor.[Group Details], ILE.[Product Group Code]";
                    particularLbl = "Proc. Market";
                    joinILE = "left join [Tyresoles (India) Pvt_ Ltd_$Ecomile Items_] as EcoItem on EcoItem.[New Serial No_] = ILE.[Serial No] --and EcoItem.[Responsibility Center] =  ILE.[Responsibility Center]\r\nleft join [Tyresoles (India) Pvt_ Ltd_$Vendor] as Vendor on Vendor.No_ = EcoItem.[Buy-from Vendor No_]";
                    particularClaims = "Item.[Product Group Code] as [Group], ISNULL(Salesperson.Name,'') as Particular,";
                    joinClaims = "Left join [Tyresoles (India) Pvt_ Ltd_$Customer] as Customer on Customer.No_ = Posted.[Customer No_]\r\n  LEFT JOIN (select * from [Tyresoles (India) Pvt_ Ltd_$Team Salesperson] where [Type] = 0) as Salesperson on Salesperson.[Team Code] = Customer.[Area Code]";
                    particularClaims2 = "Particular, [Group],";
                    particularClaimsGroup = "Particular, [Group]";
                    break;
                }
        }

        var sql = $@"
SELECT 
    CAST(SUM(-ILE.[Quantity]) AS INT) as Sold,
    {particularILE}
FROM {ileT} AS ILE
LEFT JOIN {custT} AS Cust ON Cust.[No_] = ILE.[Source No_]
{joinILE}
INNER JOIN {locT} AS Loc ON Loc.[Code] = ILE.[Location Code] {respCentersIn}
WHERE ILE.[Posting Date] >= @from AND ILE.[Posting Date] <= @to
    AND ILE.[Entry Type] = 1 
    AND Loc.[Type] IN (0, 1, 3) 
    AND ILE.[Item Category Code] = 'ECOMILE' 
    AND Cust.[Gen_ Bus_ Posting Group] = 'SALES'
GROUP BY {particularILEGroup}";

        var parameters = new Dictionary<string, object?>
        {
            ["from"] = dr.FromSql,
            ["to"] = dr.ToSql
        };

        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }

        var results = await scope.RawQueryToArrayAsync<ClaimRatio>(sql, parameters, ct).ConfigureAwait(false);
        if (results == null)
            results = Array.Empty<ClaimRatio>();

        var claimsSql = $@"
WITH LatestSettlement AS (
  SELECT 
    {particularClaims}
    Settlement.Decision,
    ROW_NUMBER() OVER (
      PARTITION BY Posted.[No_]
      ORDER BY ISNULL(Settlement.Date, '1900-01-01') DESC, Settlement.[Document No_]
    ) AS rn
  FROM {postedT} AS Posted
  LEFT JOIN {itemT} AS Item
    ON Item.No_ = Posted.[Item No_]
  LEFT JOIN {settleT} AS Settlement
    ON Settlement.[Document No_] = Posted.No_
 {joinClaims}
  WHERE Posted.[Posting Date] >= @from AND Posted.[Posting Date] <= @to
    AND Item.[Item Category Code] = 'ECOMILE'
    AND Posted.Type = 0
    {postedRespCentersIn}
)
SELECT 
    {particularClaims2}
    COUNT(*) as Claims,
    SUM(CASE WHEN Decision IN (3,5) THEN 1 ELSE 0 END) as Reject,
    SUM(CASE WHEN Decision IN (1,2,4,6,7) THEN 1 ELSE 0 END) as Pass,
    SUM(CASE WHEN Decision is null then 1 else 0 end) as Unsettled
FROM LatestSettlement
WHERE rn = 1 AND Particular IS NOT NULL
GROUP BY {particularClaimsGroup}";

        var claimStats = await scope.RawQueryToArrayAsync<ClaimRatio>(claimsSql, parameters, ct).ConfigureAwait(false);

        var saleValueSql = $@"
SELECT
    CAST(SUM(-GLEntry.[Amount]) AS DECIMAL(18, 2)) AS SaleValue
FROM {glT} AS GLEntry
INNER JOIN {custT} AS Customer ON Customer.[No_] = GLEntry.[Source No_]
WHERE GLEntry.[G_L Account No_] IN ('3126', '7573')
    AND GLEntry.[Posting Date] >= @from AND GLEntry.[Posting Date] <= @to
    {glRespCentersIn}
    AND Customer.[Gen_ Bus_ Posting Group] = 'SALES'";

        var creditNoteSql = $@"
SELECT    
    CAST(SUM(Lines.[Amount To Customer]) AS DECIMAL(18, 2)) AS CreditNoteValue
FROM {crHeaderT} AS Header
INNER JOIN {crLineT} AS Lines ON Lines.[Document No_] = Header.[No_]
INNER JOIN {postedT} AS Posted ON Posted.[No_] = Header.[External Document No_]
LEFT JOIN {itemT} AS Item ON Item.[No_] = Posted.[Item No_]
WHERE Header.[Posting Date] >= @from AND Header.[Posting Date] <= @to
    {headerRespCentersIn}
    AND Posted.[Posting Date] >= @from AND Posted.[Posting Date] <= @to
    AND Item.[Item Category Code] = 'ECOMILE'
    AND Posted.Type = 0
    {postedRespCentersIn}";

        var saleValueRows = await scope.RawQueryToArrayAsync<ClaimRatio>(saleValueSql, parameters, ct).ConfigureAwait(false);
        var creditNoteRows = await scope.RawQueryToArrayAsync<ClaimRatio>(creditNoteSql, parameters, ct).ConfigureAwait(false);

        var glSaleValueTotal = saleValueRows is { Length: > 0 } ? saleValueRows[0].SaleValue : 0m;
        var crValueTotal = creditNoteRows is { Length: > 0 } ? creditNoteRows[0].CreditNoteValue : 0m;

        static bool ProductMatch(string? a, string? b) =>
            string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

        var reportName = string.IsNullOrWhiteSpace(p.ReportName) ? "Claim Ratios" : p.ReportName;
        foreach (var r in results)
        {
            r.ReportName = reportName;
            r.Period = dr.DateRangeLabel;
            r.Locations = locations;
            r.SaleValue = glSaleValueTotal;
            r.CreditNoteValue = crValueTotal;
            r.ParticularLbl = particularLbl;
            r.View = p.View ?? "";
            r.bValue = bValue;

            r.CreditNotePercent = r.SaleValue > 0
                ? Math.Round(r.CreditNoteValue * 100m / r.SaleValue, 2)
                : 0;

            if (claimStats != null)
            {
                var stat = claimStats.FirstOrDefault(c => ProductMatch(c.Particular, r.Particular) && ProductMatch(c.Group, r.Group));
                if (stat != null)
                {
                    r.Claims = stat.Claims;
                    r.Pass = stat.Pass;
                    r.Reject = stat.Reject;
                    r.Unsettled = stat.Unsettled;
                    r.ClaimPercent = r.Sold > 0 ? Math.Round((decimal)stat.Claims * 100m / r.Sold, 2) : 0;
                    r.PassPercent = stat.Claims > 0 ? Math.Round((decimal)stat.Pass * 100m / stat.Claims, 2) : 0;
                }
            }
        }

        var list = results.ToList();
        if (claimStats != null)
        {
            foreach (var stat in claimStats)
            {
                var matchedBySale = results.Any(r =>
                    ProductMatch(r.Particular, stat.Particular) && ProductMatch(r.Group, stat.Group));
                if (matchedBySale)
                    continue;

                list.Add(new ClaimRatio
                {
                    ReportName = reportName,
                    Period = dr.DateRangeLabel,
                    Locations = locations,
                    SaleValue = glSaleValueTotal,
                    CreditNoteValue = crValueTotal,
                    ParticularLbl = particularLbl,
                    View = p.View ?? "",
                    bValue = bValue,
                    Particular = stat.Particular ?? "",
                    Group = stat.Group ?? "",
                    Sold = 0,
                    Claims = stat.Claims,
                    Pass = stat.Pass,
                    Reject = stat.Reject,
                    Unsettled = stat.Unsettled,
                    ClaimPercent = 0,
                    PassPercent = stat.Claims > 0 ? Math.Round((decimal)stat.Pass * 100m / stat.Claims, 2) : 0,
                    CreditNotePercent = glSaleValueTotal > 0
                        ? Math.Round(crValueTotal * 100m / glSaleValueTotal, 2)
                        : 0,
                });
            }
        }

        return list;
    }
    private async Task<object?> GetClaimFailureAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var settleT = scope.GetQualifiedTableName("Claim & Failure Settlement", false);
        var headerT = scope.GetQualifiedTableName("Claim & Failure Posted", false);
        var respT = scope.GetQualifiedTableName("Responsibility Center", false);
        var itemT = scope.GetQualifiedTableName("Item", false);
        var custT = scope.GetQualifiedTableName("Customer", false);
        var dealerT = scope.GetQualifiedTableName("Salesperson_Purchaser", false);
        var areaT = scope.GetQualifiedTableName("Area", false);
        var teamT = scope.GetQualifiedTableName("Team Salesperson", false);

        var typeFilter = p.Type == "Failure" ? "1" : "0";
        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Header.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        
        var sql = $@"
SELECT Header.[Item No_] as TyreSize, Item.[Product Group Code] as ProductGroup,
    Cust.[No_] as CustomerNo, Cust.[Name] as CustomerName, RespCenter.[Name] as Location,
    Dealer.Name as DealerCode, Dealer.Name as DealerName,
    Area.[Name] as AreaCode, Teams.[Name] as RegionCode, Settles.[Fault Description] as FaultText,
    CASE Settles.[Decision] 
        WHEN 0 THEN ' ' WHEN 1 THEN 'Retread' WHEN 2 THEN 'Repair' 
        WHEN 3 THEN 'Reject' WHEN 4 THEN 'CrnNote' WHEN 5 THEN 'RecToUse' 
        WHEN 6 THEN 'CasingReplace' WHEN 7 THEN 'EcomileReplace' 
    END as Decision,
    @reportTitle as ReportTitle,
    @filterText as FilterText,
    {(p.View == "Tyre-Vs-Decision" ? "1" : "0")} as TyreDecision,
    {(p.View == "Fault-Vs-Decision" ? "1" : "0")} as FaultDecision,
    {(p.View == "Customer-Vs-Decision" ? "1" : "0")} as CustDecision,
    {(p.View == "Dealer-Vs-Decision" ? "1" : "0")} as DealerDecision,
    {(p.View == "Area-Vs-Decision" ? "1" : "0")} as AreaDecision,
    {(p.View == "Region-Vs-Decision" ? "1" : "0")} as RegionDecision
FROM {settleT} as Settles
LEFT JOIN {headerT} as Header ON Header.[No_] = Settles.[Document No_]
LEFT JOIN {respT} as RespCenter ON RespCenter.[Code] = Header.[Responsibility Center]
LEFT JOIN {itemT} as Item ON Item.[No_] = Header.[Item No_]
LEFT JOIN {custT} as Cust ON Cust.[No_] = Header.[Customer No_]
LEFT JOIN {dealerT} as Dealer ON Dealer.[Code] = Cust.[Dealer Code]
LEFT JOIN {areaT} as Area ON Area.[Code] = Cust.[Area Code]
LEFT JOIN {teamT} as Teams ON Teams.[Team Code] = Area.[Team] AND Teams.[Type] = 6
WHERE Settles.[Date] BETWEEN @from AND @to 
    AND Header.[Type] = {typeFilter} 
    {respCentersIn}";

        var parameters = new Dictionary<string, object?>
        {
            ["from"] = p.From,
            ["to"] = p.To,
            ["reportTitle"] = p.View ?? "Claim & Failure",
            ["filterText"] = $"For period ( {p.From} .. {p.To} )"
        };
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }

        var results = await scope.RawQueryToArrayAsync<ClaimFailureRow>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetPostedProcOrderAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);
        var empT = scope.GetQualifiedTableName("Employee", false);
        var groupT = scope.GetQualifiedTableName("Group Details", false);

        string reportTitle = p.ReportName switch
        {
            "Posted Proc Order" => "Casing Purchase Details",
            "Posted Dispatch Order" => "Dispatch Order",
            "Posted Dispatch Details" => "Dispatch Details",
            "Casing New Numbering" => "Casing New Numbering",
            "Casing Inspection" => "Casing Inspection",
            _ => p.ReportName ?? ""
        };

        var nosIn = p.Nos?.Count > 0 ? $"({string.Join(",", p.Nos.Select((_, i) => $"@n{i}"))})" : "('')";
        var fieldToFilter = (p.ReportName == "Posted Proc Order") ? "Lines.[Document No_]" : "Lines.[Dispatch Order No_]";
        
        var makeJoin = (p.ReportName == "Casing Inspection" || p.ReportName == "Casing New Numbering")
            ? $@"LEFT JOIN {groupT} as Details ON Details.[Code] = Lines.[Make] AND Details.[Category] = 'TYREMAKE'"
            : "";
        var selectMake = (p.ReportName == "Casing Inspection" || p.ReportName == "Casing New Numbering")
            ? "Details.[Name] as Make"
            : "Lines.[Make]";

        var sql = $@"
SELECT Lines.[Document No_] as OrderNo, Lines.[Dispatch Order No_] as DispatchOrderNo,
    Lines.[Dispatch Vehicle No_] as VehicleNo, Lines.[Dispatch Mobile No] as MobileNo,
    Lines.[Dispatch Transporter] as Transporter,
    Lines.[No_] as TyreSize, Lines.[Serial No_] as SerialNo, Lines.[Direct Unit Cost] as Amount,
    Vendors.[Name] as VendorName, Vendors.[No_] as VendorNo, Vendors.[Group Details] as Location,
    Vendors.[Phone No_] as VendorMobileNo, Vendors.[Bank Name] as BankName, Vendors.[Bank A_c No] as BankAccNo,
    Vendors.[Bank IFSC Code] as BankIFSC, Vendors.[Bank Branch] as BankBranch,
    @company as CompanyName, @reportTitle as ReportName,
    CASE Lines.[Dispatch Destination] WHEN 'BEL' THEN 'Belgaum' WHEN 'JBP' THEN 'Jabalpur' WHEN 'AHM' THEN 'Ahemdabad' END as Destination,
    CASE Lines.[Casing Condition] WHEN 0 THEN '' WHEN 1 THEN 'OK' WHEN 2 THEN 'Superficial Lug damages' WHEN 3 THEN 'Minor Ply damages' WHEN 4 THEN 'Minor one cut upto BP5' WHEN 5 THEN 'Minor two cuts upto BP5' WHEN 6 THEN 'Minor three cuts upto BP5' END as Inspection,
    LTRIM(STR(CAST(RIGHT(Lines.[Document No_], 5) AS INT))) + ' / ' + LTRIM(STR(Lines.[Line No_] / 10000)) as SortNo,
    ISNULL(Employees.[Initials],'') as Inspector,
    ISNULL(Managers.[Initials],'') as Manager,
    FORMAT(Lines.[Order Date], 'dd-MMM-yyyy') as Date,
    FORMAT(Lines.[Dispatch Date], 'dd-MMM-yyyy') as DispatchDate,
    {selectMake}
FROM {lineT} as Lines
LEFT JOIN {empT} as Employees ON Employees.[No_] = Lines.[Inspector]
LEFT JOIN {vendorT} as Vendors ON Vendors.[No_] = Lines.[Buy-from Vendor No_]
LEFT JOIN {empT} as Managers ON Managers.[No_] = Vendors.[Ecomile Proc_ Mgr]
{makeJoin}
WHERE Lines.[Document Type] = 6 AND {fieldToFilter} IN {nosIn}";

        var parameters = new Dictionary<string, object?> { ["company"] = "Tyresoles (India) Pvt. Ltd.", ["reportTitle"] = reportTitle };
        if (p.Nos?.Count > 0)
        {
            for (int i = 0; i < p.Nos.Count; i++) parameters[$"n{i}"] = p.Nos[i];
        }

        var results = await scope.RawQueryToArrayAsync<PostedProcOrderRow>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetVendorBillAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);
        var respT = scope.GetQualifiedTableName("Responsibility Center", false);
        var stateT = scope.GetQualifiedTableName("State", true);

        var nosIn = p.Nos?.Count > 0 ? $"({string.Join(",", p.Nos.Select((_, i) => $"@n{i}"))})" : "('')";
        var fieldToFilter = p.Type == "Posted" ? "Line.[Document No_]" : "Line.[Dispatch Order No_]";

        var sql = $@"
SELECT Line.[Document No_] as BillNo, Line.Description as Product,
    Line.Quantity as Qty, Line.[Unit Cost (LCY)] as Rate, Line.Amount,
    Vend.[Phone No_] as MobileNo, Vend.No_ as No,
    Vend.[Bank Name] as BankName, Vend.[Bank A_c No] as BankAccNo,
    Vend.[Bank IFSC Code] as BankIFSC, Vend.[Bank Branch] as BankBranch,
    RespCenter.[Company Name] as BillToName, RespCenter.[Address] as BillToAddress,
    RespCenter.[Address 2] as BillToAddress2, RespCenter.[GST No] as BillToGSTNo,
    States.[Description] as BillToState,
    CASE WHEN Vend.[Name on Invoice] <> '' THEN Vend.[Name on Invoice] ELSE Vend.[Name] END AS [Name],
    UPPER(Vend.[Address]) as Address,
    FORMAT(Line.[Order Date], 'dd-MMM-yyyy') as Date,
    SUBSTRING(Line.[HSN_SAC Code],1,4) as HSN,
    RespCenter.[City] + ' ' + RespCenter.[Post Code] as BillToAddress3
FROM {lineT} as Line
LEFT JOIN {vendorT} as Vend ON Vend.[No_] = Line.[Buy-from Vendor No_]
LEFT JOIN {respT} as RespCenter ON RespCenter.[Code] = Line.[Responsibility Center]
LEFT JOIN {stateT} as States ON States.[Code] = RespCenter.[State]
WHERE Line.[Order Status] <= 5 AND Line.[Document Type] = 6 AND {fieldToFilter} IN {nosIn}";

        var parameters = new Dictionary<string, object?>();
        if (p.Nos?.Count > 0)
        {
            for (int i = 0; i < p.Nos.Count; i++) parameters[$"n{i}"] = p.Nos[i];
        }

        var list = await scope.RawQueryToArrayAsync<VendorBillRow>(sql, parameters, ct).ConfigureAwait(false);
        if (list == null || list.Length == 0) return null;

        var newList = new List<VendorBillRow>();
        var groupedBills = list.GroupBy(c => c.BillNo);

        foreach (var billGroup in groupedBills)
        {
            var first = billGroup.First();
            var billRows = billGroup.ToList();
            var groupedProducts = billRows.GroupBy(c => new { c.Product, c.Rate });

            foreach (var prodGroup in groupedProducts)
            {
                var row = new VendorBillRow
                {
                    No = first.No, Name = first.Name, Date = first.Date, BillNo = first.BillNo, Address = first.Address,
                    MobileNo = first.MobileNo, BillToName = first.BillToName, BillToAddress = first.BillToAddress,
                    BillToAddress2 = first.BillToAddress2, BillToAddress3 = first.BillToAddress3, BillToGSTNo = first.BillToGSTNo,
                    BillToState = first.BillToState, Product = prodGroup.Key.Product, HSN = first.HSN,
                    Qty = prodGroup.Sum(c => c.Qty), Rate = prodGroup.Key.Rate,
                    BankName = first.BankName, BankAccNo = first.BankAccNo, BankBranch = first.BankBranch, BankIFSC = first.BankIFSC
                };
                row.Amount = row.Qty * row.Rate;
                newList.Add(row);
            }

            var totalAmount = newList.Where(r => r.BillNo == first.BillNo).Sum(r => r.Amount);
            var inWords = NumberToWords((long)Math.Round(totalAmount, 0)) + " only.";
            foreach (var r in newList.Where(r => r.BillNo == first.BillNo)) r.AmountInWords = inWords;
        }

        return ToDataTable(newList);
    }

    private async Task<object?> GetCasingPurchaseDetailsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var ledgerT = scope.GetQualifiedTableName("Item Ledger Entry", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);
        var empT = scope.GetQualifiedTableName("Employee", false);
        var locT = scope.GetQualifiedTableName("Location", false);

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Loc.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var locationText = p.RespCenters?.Count > 0 ? "Location : " + string.Join(",", p.RespCenters) : "";

        var sql = $@"
SELECT Vendor.[Group Details] as Market, Vendor.[Name] as Supplier,
    Ledger.[Product Group Code] as Segment,
    FORMAT(Ledger.[Posting Date], 'dd-MMM-yy') as Date,
    ISNULL(Manager.[Initials],'') as Manager,
    SUM(Ledger.[Quantity]) as Quantity,
    'Casing Purchase Details' as ReportName,
    @locationText as Location,
    @dateRange as DateRange
FROM {ledgerT} as Ledger
LEFT JOIN {vendorT} as Vendor ON Vendor.[No_] = Ledger.[Source No_]
LEFT JOIN {empT} as Manager ON Manager.[No_] = Vendor.[Ecomile Proc_ Mgr]
LEFT JOIN {locT} as Loc ON Loc.[Code] = Ledger.[Location Code]
WHERE Ledger.[Item Category Code] = 'CASING' AND Ledger.[Entry Type] = 0
    AND Vendor.[Group Category] = 'CASING PROCUREMENT'
    AND Ledger.[Posting Date] BETWEEN @from AND @to
    {respCentersIn}
GROUP BY Ledger.[Posting Date], Ledger.[Source No_], Ledger.[Product Group Code], Vendor.[Name], Vendor.[Group Details], Manager.[Initials]";

        var dr = ResolveCasingPurchaseSqlAndDisplayRange(p);
        var parameters = new Dictionary<string, object?>
        {
            ["from"] = dr.FromSql,
            ["to"] = dr.ToSql,
            ["locationText"] = locationText,
            ["dateRange"] = dr.DateRangeLabel
        };
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }

        var results = await scope.RawQueryToArrayAsync<CasingPurchaseDetailsRow>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetCasingPurchaseAnalysisAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var ledgerT = scope.GetQualifiedTableName("Item Ledger Entry", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);
        var empT = scope.GetQualifiedTableName("Employee", false);
        var valueT = scope.GetQualifiedTableName("Value Entry", false);
        var locT = scope.GetQualifiedTableName("Location", false);

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Loc.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var locationText = p.RespCenters?.Count > 0 ? "Location : " + string.Join(",", p.RespCenters) : "";

        var sql = $@"
SELECT Vendor.[Group Details] as Market, Vendor.[Name] as Supplier, Vendor.[No_] as SupplierCode, Vendor.[City],
    Ledger.[Product Group Code] as Segment, Ledger.[Item No_] as Tyre,
    ISNULL(Manager.[Initials],'') as Manager,
    SUM(Ledger.[Quantity]) as Quantity,
    SUM(ValueLedger.[Cost Amount (Actual)]) as Amount,
    'Casing Purchase Analysis' as ReportName,
    @locationText as Location,
    @dateRange as DateRange
FROM {ledgerT} as Ledger
LEFT JOIN {vendorT} as Vendor ON Vendor.[No_] = Ledger.[Source No_]
LEFT JOIN {empT} as Manager ON Manager.[No_] = Vendor.[Ecomile Proc_ Mgr]
LEFT JOIN {valueT} as ValueLedger ON ValueLedger.[Item Ledger Entry No_] = Ledger.[Entry No_]
LEFT JOIN {locT} as Loc ON Loc.[Code] = Ledger.[Location Code]
WHERE Ledger.[Item Category Code] = 'CASING' AND Ledger.[Entry Type] = 0
    AND Vendor.[Group Category] = 'CASING PROCUREMENT'
    AND Ledger.[Posting Date] BETWEEN @from AND @to
    {respCentersIn}
GROUP BY Ledger.[Item No_], Ledger.[Source No_], Ledger.[Product Group Code], Vendor.[Name], Vendor.[No_], Vendor.[City], Vendor.[Group Details], Manager.[Initials]
ORDER BY Manager.[Initials], Vendor.[Group Details]";

        var dr = ResolveCasingPurchaseSqlAndDisplayRange(p);
        var parameters = new Dictionary<string, object?>
        {
            ["from"] = dr.FromSql,
            ["to"] = dr.ToSql,
            ["locationText"] = locationText,
            ["dateRange"] = dr.DateRangeLabel
        };
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }

        var results = await scope.RawQueryToArrayAsync<CasingPurchaseAnalysisRow>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetEcomileInvSalesStatisticsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var ledgerT = scope.GetQualifiedTableName("Item Ledger Entry", false);
        var locT = scope.GetQualifiedTableName("Location", false);

        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Loc.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var locationText = p.RespCenters?.Count > 0 ? "Location : " + string.Join(",", p.RespCenters) : "";

        var sql = $@"
SELECT Ledger.[Item No_] as Tyre, Ledger.[Variant Code] as Variant,
    -SUM(Ledger.[Quantity]) as Quantity,
    'Ecomile Inv. Sales Statistics' as ReportName,
    @locationText as Location,
    @dateRange as DateRange
FROM {ledgerT} as Ledger
LEFT JOIN {locT} as Loc ON Loc.[Code] = Ledger.[Location Code]
WHERE Ledger.[Item Category Code] = 'ECOMILE' AND Ledger.[Entry Type] = 1
    AND Ledger.[Posting Date] BETWEEN @from AND @to
    {respCentersIn}
GROUP BY Ledger.[Item No_], Ledger.[Variant Code]
ORDER BY Ledger.[Item No_], Ledger.[Variant Code]";

        var parameters = new Dictionary<string, object?>
        {
            ["from"] = p.From ?? DateTime.Now.AddMonths(-1).ToString("yyyy-MM-01"),
            ["to"] = p.To ?? DateTime.Now.ToString("yyyy-MM-dd"),
            ["locationText"] = locationText,
            ["dateRange"] = $"For period {p.From} .. {p.To}"
        };
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }

        var results = await scope.RawQueryToArrayAsync<EcomileInvSalesStatisticsRow>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetPostedDispatchAverageCostAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var lineT = scope.GetQualifiedTableName("Purchase Line", false);
        var vendorT = scope.GetQualifiedTableName("Vendor", false);

        var nosIn = p.Nos?.Count > 0 ? $"({string.Join(",", p.Nos.Select((_, i) => $"@n{i}"))})" : "('')";
        var respCentersIn = p.RespCenters?.Count > 0 ? $" AND Lines.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";

        var sql = $@"
SELECT Lines.[No_] as TyreSize,
    AVG(Lines.[Direct Unit Cost]) as AvgCost,
    SUM(Lines.[Quantity]) as Qty,
    SUM(Lines.[Line Amount]) as TotalCost,
    @company as CompanyName,
    'Casing Average Cost' as ReportName,
    Vendors.[Group Details] as Source,
    Lines.[Dispatch Order No_] as DocumentNo,
    FORMAT(Lines.[Dispatch Date], 'dd-MMM-yyyy') as Date,
    CASE Lines.[Dispatch Destination] WHEN 'BEL' THEN 'Belgaum' WHEN 'JBP' THEN 'Jabalpur' WHEN 'AHM' THEN 'Ahemdabad' END as Destination
FROM {lineT} as Lines
LEFT JOIN {vendorT} as Vendors ON Vendors.[No_] = Lines.[Buy-from Vendor No_]
WHERE Lines.[Document Type] = 6 AND Lines.[Dispatch Order No_] IN {nosIn} {respCentersIn}
GROUP BY Lines.[No_], Lines.[Dispatch Date], Vendors.[Group Details], Lines.[Dispatch Destination], Lines.[Dispatch Order No_]";

        var parameters = new Dictionary<string, object?> { ["company"] = "Tyresoles (India) Pvt. Ltd." };
        if (p.Nos?.Count > 0)
        {
            for (int i = 0; i < p.Nos.Count; i++) parameters[$"n{i}"] = p.Nos[i];
        }
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) parameters[$"rc{i}"] = p.RespCenters[i];
        }

        var results = await scope.RawQueryToArrayAsync<PostedShipmentAverageCostRow>(sql, parameters, ct).ConfigureAwait(false);
        return ToDataTable(results);
    }

    private async Task<object?> GetClaimAnalysisAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        // This is a complex report involving multiple sub-queries.
        // We port the logic of GetClaimAnalysisRecordsFromClaims and GetClaimAnalysisRecordsFromItemLedger.
        
        DateTime fromDate = DateTime.TryParse(p.From, out var f) ? new DateTime(f.Year, f.Month, 1) : DateTime.Now.AddMonths(-1);
        DateTime toDate = DateTime.TryParse(p.To, out var t) ? t : DateTime.Now;
        if (toDate > DateTime.Now) toDate = DateTime.Now;
        // Last of month
        toDate = new DateTime(toDate.Year, toDate.Month, DateTime.DaysInMonth(toDate.Year, toDate.Month));

        int totalMonths = ((toDate.Year - fromDate.Year) * 12) + toDate.Month - fromDate.Month + 1;
        if (totalMonths > 12) totalMonths = 12;

        var result = new List<ClaimAnalysisRow>();
        var locationText = p.RespCenters?.Count > 0 ? string.Join(",", p.RespCenters) : "";

        // Ported sizes and groups
        var casingItems = new[]
        {
            (No: "1000*20", Type: "A"), (No: "1000*20 (18 PLY)", Type: "A"), (No: "1000R20", Type: "B"),
            (No: "1100*20", Type: "C"), (No: "1100*20 (18 PLY)", Type: "C"), (No: "1100R20", Type: "C"),
            (No: "11R22.5", Type: "C"), (No: "1200*20", Type: "C"), (No: "1200R20", Type: "C"),
            (No: "750*16", Type: "C"), (No: "825*20", Type: "C"), (No: "900*20", Type: "C"), (No: "900R20", Type: "C")
        };
        var types = casingItems.Select(c => c.Type).Distinct().ToList();

        foreach (var type in types)
        {
            var nos = casingItems.Where(c => c.Type == type).Select(c => c.No).ToList();
            var claimRecords = new List<ClaimAnalysisRecordRow>();
            var ledgerRecords = new List<ClaimAnalysisRecordRow>();

            for (int i = 0; i < totalMonths; i++)
            {
                var mStart = fromDate.AddMonths(i);
                var mEnd = new DateTime(mStart.Year, mStart.Month, DateTime.DaysInMonth(mStart.Year, mStart.Month));
                var periodStr = $"{mStart:dd-MMM-yy} .. {mEnd:dd-MMM-yy}";
                
                // From Claims
                var claims = await GetClaimAnalysisRecordsFromClaimsAsync(scope, p, mStart, mEnd, type, nos, ct).ConfigureAwait(false);
                claimRecords.AddRange(claims);

                // From Ledger (Past 12 months average)
                var pStart = mStart.AddMonths(-12);
                var ledger = await GetClaimAnalysisRecordsFromItemLedgerAsync(scope, p, pStart, mEnd, type, nos, ct).ConfigureAwait(false);
                ledgerRecords.AddRange(ledger);
            }

            var groupedByTopic = claimRecords.GroupBy(c => c.Topic);
            foreach (var topicGroup in groupedByTopic)
            {
                var topic = topicGroup.Key;
                var row = new ClaimAnalysisRow
                {
                    ReportName = "Claim Analysis",
                    Period = $"{fromDate.ToString("dd-MMM-yy")} .. {toDate.ToString("dd-MMM-yy")}",
                    Location = locationText,
                    View = p.View ?? "", ParticularLabel = p.View ?? "",
                    Particular = topic, Type = p.Type ?? "",
                    Size = type switch { "A" => "1000*20", "B" => "1000R20", "C" => "Others", _ => "" }
                };

                decimal totalA = 0, totalTM = 0;
                for (int i = 0; i < totalMonths; i++)
                {
                    var mStart = fromDate.AddMonths(i);
                    var mEnd = new DateTime(mStart.Year, mStart.Month, DateTime.DaysInMonth(mStart.Year, mStart.Month));
                    var periodStr = $"{mStart:dd-MMM-yy} .. {mEnd:dd-MMM-yy}";
                    var periodRYStr = $"{mStart.AddMonths(-12):dd-MMM-yy} .. {mEnd:dd-MMM-yy}";
                    
                    var figure = topicGroup.FirstOrDefault(c => c.Period == periodStr)?.Figure ?? 0;
                    var figureRY = ledgerRecords.FirstOrDefault(c => c.Period == periodRYStr && c.Topic == topic)?.Figure ?? 0;
                    var tmFigure = Math.Round(figureRY / 12m, 0);

                    // Fault reasons logic fallback for TM
                    if ((p.View == "Fault Reasons" || p.View == "Prod. Agings") && i < 1) // Simplified
                         tmFigure = Math.Round(ledgerRecords.Where(c => c.Period == periodRYStr).Sum(c => c.Figure) / 12m, 0);

                    var pm = tmFigure > 0 ? (figure / tmFigure) * 100 : 0;
                    totalA += figure; totalTM += tmFigure;

                    var monthStr = mStart.ToString("MMM - yy");
                    switch (i)
                    {
                        case 0: row.Month1 = monthStr; row.A1 = figure; row.TM1 = tmFigure; row.PM1 = pm; break;
                        case 1: row.Month2 = monthStr; row.A2 = figure; row.TM2 = tmFigure; row.PM2 = pm; break;
                        case 2: row.Month3 = monthStr; row.A3 = figure; row.TM3 = tmFigure; row.PM3 = pm; break;
                        case 3: row.Month4 = monthStr; row.A4 = figure; row.TM4 = tmFigure; row.PM4 = pm; break;
                        case 4: row.Month5 = monthStr; row.A5 = figure; row.TM5 = tmFigure; row.PM5 = pm; break;
                        case 5: row.Month6 = monthStr; row.A6 = figure; row.TM6 = tmFigure; row.PM6 = pm; break;
                        case 6: row.Month7 = monthStr; row.A7 = figure; row.TM7 = tmFigure; row.PM7 = pm; break;
                        case 7: row.Month8 = monthStr; row.A8 = figure; row.TM8 = tmFigure; row.PM8 = pm; break;
                        case 8: row.Month9 = monthStr; row.A9 = figure; row.TM9 = tmFigure; row.PM9 = pm; break;
                        case 9: row.Month10 = monthStr; row.A10 = figure; row.TM10 = tmFigure; row.PM10 = pm; break;
                        case 10: row.Month11 = monthStr; row.A11 = figure; row.TM11 = tmFigure; row.PM11 = pm; break;
                        case 11: row.Month12 = monthStr; row.A12 = figure; row.TM12 = tmFigure; row.PM12 = pm; break;
                    }
                }
                row.TTA = totalA; row.TTM = totalTM; row.TTPM = totalTM > 0 ? (totalA / totalTM) * 100 : 0;
                result.Add(row);
            }
        }
        return ToDataTable(result.OrderBy(r => r.Particular).ToList());
    }

    private async Task<List<ClaimAnalysisRecordRow>> GetClaimAnalysisRecordsFromClaimsAsync(ITenantScope scope, SalesReportParams p, DateTime from, DateTime to, string type, List<string> nos, CancellationToken ct)
    {
        var headerT = scope.GetQualifiedTableName("Claim & Failure Posted", false);
        var itemT = scope.GetQualifiedTableName("Item", false);
        var settleT = scope.GetQualifiedTableName("Claim & Failure Settlement", false);
        var respIn = p.RespCenters?.Count > 0 ? $" AND Header.[Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))})" : "";
        var nosIn = nos.Count > 0 ? $"({string.Join(",", nos.Select((_, i) => $"@n{i}"))})" : "('')";
        var decisionFilter = p.Type == "Claim Sanctioned" ? " AND Settle.[Decision] IN (1, 4)" : "";

        string selectTopic, joinTopic, groupTopic;
        switch (p.View)
        {
            case "Customers":
                selectTopic = "C.[Name] as Topic";
                joinTopic = $"LEFT JOIN {scope.GetQualifiedTableName("Customer", false)} C ON C.[No_] = Header.[Customer No_]";
                groupTopic = "C.[Name]";
                break;
            case "Dealers":
                selectTopic = "D.[Name] as Topic";
                joinTopic = $@"LEFT JOIN {scope.GetQualifiedTableName("Customer", false)} C ON C.[No_] = Header.[Customer No_] 
                               LEFT JOIN {scope.GetQualifiedTableName("Salesperson_Purchaser", false)} D ON D.[Code] = C.[Dealer Code]";
                groupTopic = "D.[Name]";
                break;
            case "Fault Reasons":
                selectTopic = "Settle.[Fault Description] as Topic";
                joinTopic = "";
                groupTopic = "Settle.[Fault Description]";
                break;
            case "Tread Patterns":
                selectTopic = "INV.[Pattern] as Topic";
                joinTopic = $"LEFT JOIN {scope.GetQualifiedTableName("Sales Invoice Line", false)} INV ON INV.[Document No_] = Header.[Invoice No_] AND INV.[Line No_] = Header.[Line No_]";
                groupTopic = "INV.[Pattern]";
                break;
            case "Casing Makes":
                selectTopic = "Header.[Make] as Topic";
                joinTopic = "";
                groupTopic = "Header.[Make]";
                break;
            case "Prod. Agings":
                selectTopic = @"CASE 
                    WHEN Header.[Run Period] <= 30 THEN '1) Less than 30 days' 
                    WHEN Header.[Run Period] <= 45 THEN '2) 31 to 45 days' 
                    WHEN Header.[Run Period] <= 90 THEN '3) 46 to 90 days' 
                    WHEN Header.[Run Period] <= 180 THEN '4) 91 to 180 days' 
                    WHEN Header.[Run Period] <= 270 THEN '5) 181 to 270 days' 
                    WHEN Header.[Run Period] <= 365 THEN '6) 271 to 365 days' 
                    ELSE '7) above 365 days' END as Topic";
                joinTopic = "";
                groupTopic = selectTopic.Replace("as Topic", "");
                break;
            default:
                selectTopic = "'' as Topic"; joinTopic = ""; groupTopic = "''";
                break;
        }

        var sql = $@"
SELECT {selectTopic}, COUNT(Header.[No_]) as Figure, @period as Period, @type as Type
FROM {headerT} as Header
LEFT JOIN {settleT} as Settle ON Settle.[Document No_] = Header.[No_]
{joinTopic}
WHERE Header.[Posting Date] BETWEEN @from AND @to 
    AND Header.[Item No_] IN (SELECT [No_] FROM {itemT} WHERE [Item Category Code] = 'ECOMILE' AND [Alternative Item No_] IN {nosIn})
    AND Header.[Type] = 0 {respIn} {decisionFilter}
GROUP BY {groupTopic}";

        var pars = new Dictionary<string, object?> { ["from"] = from, ["to"] = to, ["period"] = $"{from:dd-MMM-yy} .. {to:dd-MMM-yy}", ["type"] = type };
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) pars[$"rc{i}"] = p.RespCenters[i];
        }
        for (int i = 0; i < nos.Count; i++) pars[$"n{i}"] = nos[i];

        return (await scope.RawQueryToArrayAsync<ClaimAnalysisRecordRow>(sql, pars, ct).ConfigureAwait(false)).ToList();
    }

    private async Task<List<ClaimAnalysisRecordRow>> GetClaimAnalysisRecordsFromItemLedgerAsync(ITenantScope scope, SalesReportParams p, DateTime from, DateTime to, string type, List<string> nos, CancellationToken ct)
    {
        var ledgerT = scope.GetQualifiedTableName("Item Ledger Entry", false);
        var itemT = scope.GetQualifiedTableName("Item", false);
        var respIn = p.RespCenters?.Count > 0 ? $" AND L.[Location Code] IN (SELECT [Code] FROM {scope.GetQualifiedTableName("Location", false)} WHERE [Responsibility Center] IN ({string.Join(",", p.RespCenters.Select((_, i) => $"@rc{i}"))}) AND [Type] = 0)" : "";
        var nosIn = nos.Count > 0 ? $"({string.Join(",", nos.Select((_, i) => $"@n{i}"))})" : "('')";

        string selectTopic, joinTopic, groupTopic;
        switch (p.View)
        {
            case "Customers":
                selectTopic = "C.[Name] as Topic";
                joinTopic = $"LEFT JOIN {scope.GetQualifiedTableName("Customer", false)} C ON C.[No_] = L.[Source No_]";
                groupTopic = "C.[Name]";
                break;
            case "Dealers":
                selectTopic = "D.[Name] as Topic";
                joinTopic = $@"LEFT JOIN {scope.GetQualifiedTableName("Customer", false)} C ON C.[No_] = L.[Source No_] 
                               LEFT JOIN {scope.GetQualifiedTableName("Salesperson_Purchaser", false)} D ON D.[Code] = C.[Dealer Code]";
                groupTopic = "D.[Name]";
                break;
            case "Tread Patterns":
                selectTopic = "V.[Pattern] as Topic";
                joinTopic = $"LEFT JOIN {scope.GetQualifiedTableName("Item Variant", false)} V ON V.[Item No_] = L.[Item No_] AND V.[Code] = L.[Variant Code]";
                groupTopic = "V.[Pattern]";
                break;
            case "Casing Makes":
                selectTopic = "L.[Make] as Topic";
                joinTopic = "";
                groupTopic = "L.[Make]";
                break;
            default:
                selectTopic = "'' as Topic"; joinTopic = ""; groupTopic = "''";
                break;
        }

        var sql = $@"
SELECT {selectTopic}, -SUM(L.[Quantity]) as Figure, @period as Period, @type as Type
FROM {ledgerT} as L
{joinTopic}
WHERE L.[Item Category Code] = 'ECOMILE' AND L.[Entry Type] = 1 AND L.[Document Type] IN (1, 4)
    AND L.[Posting Date] BETWEEN @from AND @to
    AND L.[Item No_] IN (SELECT [No_] FROM {itemT} WHERE [Item Category Code] = 'ECOMILE' AND [Alternative Item No_] IN {nosIn})
    {respIn}
GROUP BY {groupTopic}";

        var pars = new Dictionary<string, object?> { ["from"] = from, ["to"] = to, ["period"] = $"{from:dd-MMM-yy} .. {to:dd-MMM-yy}", ["type"] = type };
        if (p.RespCenters?.Count > 0)
        {
            for (int i = 0; i < p.RespCenters.Count; i++) pars[$"rc{i}"] = p.RespCenters[i];
        }
        for (int i = 0; i < nos.Count; i++) pars[$"n{i}"] = nos[i];

        return (await scope.RawQueryToArrayAsync<ClaimAnalysisRecordRow>(sql, pars, ct).ConfigureAwait(false)).ToList();
    }

    #endregion

    #region Helpers

    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));
        string words = "";
        if ((number / 10000000) > 0) { words += NumberToWords(number / 10000000) + " Crore "; number %= 10000000; }
        if ((number / 100000) > 0) { words += NumberToWords(number / 100000) + " Lakh "; number %= 100000; }
        if ((number / 1000) > 0) { words += NumberToWords(number / 1000) + " Thousand "; number %= 1000; }
        if ((number / 100) > 0) { words += NumberToWords(number / 100) + " Hundred "; number %= 100; }
        if (number > 0)
        {
            if (words != "") words += "and ";
            var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
            if (number < 20) words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0) words += "-" + unitsMap[number % 10];
            }
        }
        return words.Trim();
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
                var pt = p[i].PropertyType;
                if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(Nullable<>)) pt = Nullable.GetUnderlyingType(pt)!;
                types[i] = pt == typeof(DateTime) ? typeof(DateTime) : pt == typeof(decimal) ? typeof(decimal) : pt == typeof(int) ? typeof(int) : pt == typeof(bool) ? typeof(bool) : typeof(string);
            }
            return (p, types);
        });
        var dt = new DataTable();
        for (int i = 0; i < props.Length; i++) dt.Columns.Add(props[i].Name, colTypes[i]);
        foreach (var r in rows)
        {
            var row = dt.NewRow();
            for (int i = 0; i < props.Length; i++) row[props[i].Name] = props[i].GetValue(r) ?? DBNull.Value;
            dt.Rows.Add(row);
        }
        return dt;
    }

    #endregion
}
