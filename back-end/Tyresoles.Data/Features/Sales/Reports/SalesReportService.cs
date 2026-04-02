using System.Data;
using System.Linq;
using Dataverse.NavLive;
using Tyresoles.Data.Features.Sales.Reports.Models;
using Tyresoles.Data.Constants;
using Tyresoles.Reporting.Abstractions;
using Tyresoles.Sql.Abstractions;
using System.Globalization;
using System.Collections.Generic;
using ReportBankAccount = Tyresoles.Data.Features.Sales.Reports.Models.BankAccount;

namespace Tyresoles.Data.Features.Sales.Reports;

/// <summary>
/// Uses Tyresoles.Sql to fetch report data and Tyresoles.Reporting to render RDLC reports.
/// Implements all sales reports from legacy Sales.cs starting with PostedSalesInvoice.
/// </summary>
public sealed class SalesReportService : ISalesReportService
{
    private readonly IReportRenderer _reportRenderer;

    public SalesReportService(IReportRenderer reportRenderer)
    {
        _reportRenderer = reportRenderer ?? throw new ArgumentNullException(nameof(reportRenderer));
    }

    /// <inheritdoc />
    public async Task<byte[]> RenderReportAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportName))
            throw new ArgumentException("Report name is required.", nameof(reportName));
        ArgumentNullException.ThrowIfNull(parameters);

        // If multiple Nos are provided, prioritize them for filtering (e.g. for multi-print)
        if (parameters.Nos != null && parameters.Nos.Count > 0)
        {
            // For single doc reports like "Posted Sales Invoice", we might be printing multiple.
            // The renderer/data fetchers need to handle this.
            // Map common report names to their data fetchers which should be updated to handle List<string> Nos.
        }

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

    /// <inheritdoc />
    public async Task<DocumentDto[]> GetMyDocuments(
        ITenantScope scope,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default)
    {
        var p = parameters ?? new SalesReportParams();
        var view = p.View ?? "Invoice";
        var type = p.Type ?? "";

        var today = DateTime.Today;
        var fromDate = DateTime.TryParse(p.From, out var fd) ? fd : new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        var toDate = DateTime.TryParse(p.To, out var td) ? td : today;
        var toDateInclusive = toDate.Date.AddDays(1);

        // Fetch context list if filtering by Partner, PartnerGroup or Employee. Use raw Where to avoid closure capture.
        string[] dealerCodes = Array.Empty<string>();
        string[] customerNos = Array.Empty<string>();

        if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
        {
            if (string.Equals(p.EntityType, EntityTypes.Partner, StringComparison.OrdinalIgnoreCase))
            {
                dealerCodes = [p.EntityCode];
                var qry = scope.Query<Customer>().Where("[Dealer Code] = @code", new { code = p.EntityCode }).Select(c => c.No);
                customerNos = await scope.ToArrayAsync(qry, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(p.EntityType, EntityTypes.PartnerGroup, StringComparison.OrdinalIgnoreCase))
            {
                var qryDealer = scope.Query<SalespersonPurchaser>().Where("[Group] = @code", new { code = p.EntityCode }).Select(d => d.Code);
                dealerCodes = await scope.ToArrayAsync(qryDealer, cancellationToken).ConfigureAwait(false);
                if (dealerCodes.Length > 0)
                {
                    var (sql, prms) = BuildInClause("[Dealer Code]", "d", dealerCodes);
                    var qryCust = scope.Query<Customer>().Where(sql, prms).Select(c => c.No);
                    customerNos = await scope.ToArrayAsync(qryCust, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (string.Equals(p.EntityType, EntityTypes.Employee, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(p.EntityDepartment, Departments.Sales, StringComparison.OrdinalIgnoreCase))
                {
                    var qryTeam = scope.Query<TeamSalesperson>().Where("[Code] = @code", new { code = p.EntityCode }).Select(t => t.TeamCode);
                    var teams = await scope.ToArrayAsync(qryTeam, cancellationToken).ConfigureAwait(false);
                    if (teams.Length > 0)
                    {
                        var (teamInSql, teamPrms) = BuildInClause("[Team]", "t", teams);
                        var qryArea = scope.Query<Area>().Where(teamInSql, teamPrms).Select(a => a.Code);
                        var areas = await scope.ToArrayAsync(qryArea, cancellationToken).ConfigureAwait(false);
                        if (areas.Length > 0)
                        {
                            var (areaInSql, areaPrms) = BuildInClause("[Area Code]", "a", areas);
                            var qryCust = scope.Query<Customer>().Where(areaInSql, areaPrms).Select(c => c.No);
                            customerNos = await scope.ToArrayAsync(qryCust, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        var customerNosFromFilter = SalesReportParams.ParseCustomerNos(p.Customers);
        var hasExplicitCustomerFilter = customerNosFromFilter.Length > 0;
        if (customerNosFromFilter.Length > 0)
            customerNos = customerNosFromFilter;

        if (view == "Claim")
        {
            var query = scope.Query<ClaimFailurePosted>()
                .Where(h => h.Type == 0) // Claim
                .Where(h => h.PostingDate >= fromDate && h.PostingDate < toDateInclusive);

            

            if (p.Nos?.Count > 0)
            {
                var (sql, prms) = BuildInClause("[No_]", "no", p.Nos);
                query = query.Where(sql, prms);
            }
            if (p.RespCenters?.Count > 0)
            {
                var (sql, prms) = BuildInClause("[Responsibility Center]", "rc", p.RespCenters);
                query = query.Where(sql, prms);
            }

            // User context filtering for Claims (raw Where to avoid closure capture)
            if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
            {
                if (string.Equals(p.EntityType, EntityTypes.Customer, StringComparison.OrdinalIgnoreCase))
                    query = query.Where("[Customer No_] = @code", new { code = p.EntityCode });
                else if (string.Equals(p.EntityType, EntityTypes.Employee, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(p.EntityDepartment, Departments.Sales, StringComparison.OrdinalIgnoreCase))
                    {
                        if (customerNos.Length == 0)
                            query = query.Where("1=0", null);
                        else
                        {
                            var (sql, prms) = BuildInClause("[Customer No_]", "cust", customerNos);
                            query = query.Where(sql, prms);
                        }
                    }
                }
            }

            var take = p.Take ?? 50;
            var skip = p.Skip ?? 0;
            var claims = await scope.ToArrayAsync(query.OrderByDescending(h => h.PostingDate).Skip(skip).Take(take), cancellationToken).ConfigureAwait(false);
            return claims.Select(c => new DocumentDto
            {
                No = c.No,
                Date = c.PostingDate,
                CustomerNo = c.CustomerNo,
                Name = c.Name,
                Amount = c.CompensationAmount
            }).ToArray();
        }
        else if (view == "CrNote")
        {
            var query = scope.Query<SalesCrMemoHeader>()
                .Where(h => h.PostingDate >= fromDate && h.PostingDate < toDateInclusive);

            if (!string.IsNullOrEmpty(type) && type == "GST")
                query = query.Where(c => c.GSTCustomerType == 1).Where(c => c.EInvIRNNo != "");

            if (p.Nos?.Count > 0)
            {
                var (sql, prms) = BuildInClause("[No_]", "no", p.Nos);
                query = query.Where(sql, prms);
            }
            if (p.RespCenters?.Count > 0)
            {
                var (sql, prms) = BuildInClause("[Responsibility Center]", "rc", p.RespCenters);
                query = query.Where(sql, prms);
            }

            // User context filtering (raw Where to avoid closure capture)
            if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
            {
                if (string.Equals(p.EntityType, EntityTypes.Customer, StringComparison.OrdinalIgnoreCase))
                    query = query.Where("[Sell-to Customer No_] = @code", new { code = p.EntityCode });
                else if (string.Equals(p.EntityType, EntityTypes.Partner, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(p.EntityType, EntityTypes.PartnerGroup, StringComparison.OrdinalIgnoreCase))
                {
                    if (dealerCodes.Length == 0)
                        query = query.Where("1=0", null);
                    else
                    {
                        var (sql, prms) = BuildInClause("[Dealer Code]", "dlr", dealerCodes);
                        query = query.Where(sql, prms);
                    }
                }
                else if (string.Equals(p.EntityType, EntityTypes.Employee, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(p.EntityDepartment, Departments.Sales, StringComparison.OrdinalIgnoreCase))
                    {
                        if (customerNos.Length == 0)
                            query = query.Where("1=0", null);
                        else
                        {
                            var (sql, prms) = BuildInClause("[Sell-to Customer No_]", "cust", customerNos);
                            query = query.Where(sql, prms);
                        }
                    }
                }
            }

            if (hasExplicitCustomerFilter && customerNos.Length > 0)
            {
                var partnerScope = string.Equals(p.EntityType, EntityTypes.Partner, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.EntityType, EntityTypes.PartnerGroup, StringComparison.OrdinalIgnoreCase);
                if (partnerScope)
                {
                    var (custSql, custPrms) = BuildInClause("[Sell-to Customer No_]", "sellto", customerNos);
                    query = query.Where(custSql, custPrms);
                }
            }

            var take = p.Take ?? 50;
            var skip = p.Skip ?? 0;
            var headers = await scope.ToArrayAsync(query.OrderByDescending(h => h.PostingDate).Skip(skip).Take(take), cancellationToken).ConfigureAwait(false);
            if (headers.Length == 0) return Array.Empty<DocumentDto>();

            var docNos = headers.Select(h => h.No).ToArray();
            var (docNoSql, docNoPrms) = BuildInClause("[Document No_]", "doc", docNos);
            var lines = await scope.Query<SalesCrMemoLine>()
                .Where(docNoSql, docNoPrms)
                .Where(l => l.Quantity > 0 && l.No != "9400")
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);

            var amountMap = lines.GroupBy(l => l.DocumentNo).ToDictionary(g => g.Key, g => g.Sum(l => l.AmountToCustomer));

            return headers.Select(h => new DocumentDto
            {
                No = h.No,
                Date = h.PostingDate,
                CustomerNo = h.SellToCustomerNo,
                Name = h.SellToCustomerName,
                Amount = amountMap.TryGetValue(h.No, out var amt) ? amt : 0
            }).ToArray();
        }
        else // Invoice
        {
            var query = scope.Query<SalesInvoiceHeader>()
                .Where(h => h.PostingDate >= fromDate && h.PostingDate < toDateInclusive);

            if (!string.IsNullOrEmpty(type) && type == "GST")
                query = query.Where(c => c.GSTCustomerType == 1).Where(c => c.EInvIRNNo != "");

            if (p.Nos?.Count > 0)
            {
                var (sql, prms) = BuildInClause("[No_]", "no", p.Nos);
                query = query.Where(sql, prms);
            }
            if (p.RespCenters?.Count > 0)
            {
                var (sql, prms) = BuildInClause("[Responsibility Center]", "rc", p.RespCenters);
                query = query.Where(sql, prms);
            }

            // User context filtering (raw Where to avoid closure capture)
            if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
            {
                if (string.Equals(p.EntityType, EntityTypes.Customer, StringComparison.OrdinalIgnoreCase))
                    query = query.Where("[Sell-to Customer No_] = @code", new { code = p.EntityCode });
                else if (string.Equals(p.EntityType, EntityTypes.Partner, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(p.EntityType, EntityTypes.PartnerGroup, StringComparison.OrdinalIgnoreCase))
                {
                    if (dealerCodes.Length == 0)
                        query = query.Where("1=0", null);
                    else
                    {
                        var (sql, prms) = BuildInClause("[Dealer Code]", "dlr", dealerCodes);
                        query = query.Where(sql, prms);
                    }
                }
                else if (string.Equals(p.EntityType, EntityTypes.Employee, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(p.EntityDepartment, Departments.Sales, StringComparison.OrdinalIgnoreCase))
                    {
                        if (customerNos.Length == 0)
                            query = query.Where("1=0", null);
                        else
                        {
                            var (sql, prms) = BuildInClause("[Sell-to Customer No_]", "cust", customerNos);
                            query = query.Where(sql, prms);
                        }
                    }
                }
            }

            if (hasExplicitCustomerFilter && customerNos.Length > 0)
            {
                var partnerScope = string.Equals(p.EntityType, EntityTypes.Partner, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.EntityType, EntityTypes.PartnerGroup, StringComparison.OrdinalIgnoreCase);
                if (partnerScope)
                {
                    var (custSql, custPrms) = BuildInClause("[Sell-to Customer No_]", "sellto", customerNos);
                    query = query.Where(custSql, custPrms);
                }
            }

            var take = p.Take ?? 50;
            var skip = p.Skip ?? 0;
            var headers = await scope.ToArrayAsync(query.OrderByDescending(h => h.PostingDate).Skip(skip).Take(take), cancellationToken).ConfigureAwait(false);
            if (headers.Length == 0) return Array.Empty<DocumentDto>();

            var docNos = headers.Select(h => h.No).ToArray();
            var (docNoSql, docNoPrms) = BuildInClause("[Document No_]", "doc", docNos);
            var lines = await scope.Query<SalesInvoiceLine>()
                .Where(docNoSql, docNoPrms)
                .Where(l => l.ItemCategoryCode == "ECOMILE" || l.ItemCategoryCode == "RETD")
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);

            var amountMap = lines.GroupBy(l => l.DocumentNo).ToDictionary(g => g.Key, g => g.Sum(l => l.AmountToCustomer));

            return headers.Select(h => new DocumentDto
            {
                No = h.No,
                Date = h.PostingDate,
                CustomerNo = h.SellToCustomerNo,
                Name = h.SellToCustomerName,
                Amount = amountMap.TryGetValue(h.No, out var amt) ? amt : 0
            }).ToArray();
        }
    }

    /// <summary>Builds "column IN (@p0, @p1, ...)" and a parameter dictionary to avoid closure capture in expression trees (prevents InvalidProgramException).</summary>
    private static (string sql, Dictionary<string, object> parameters) BuildInClause(string quotedColumn, string paramPrefix, IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
            return ("1=0", new Dictionary<string, object>());
        var dict = new Dictionary<string, object>();
        var names = new List<string>();
        for (int i = 0; i < values.Count; i++)
        {
            var key = "@" + paramPrefix + i;
            names.Add(key);
            dict[key] = values[i];
        }
        return ($"{quotedColumn} IN ({string.Join(", ", names)})", dict);
    }

    private static readonly List<ReportMeta> ReportMetaList = new()
    {
        new() { Id = 1, Name = "Sales & Balance", ShowDealers = true, ShowAreas = true, ShowType = true, ShowView = true, ShowRegions = true, TypeOptions = new List<string> { "Retread-Ecomile", "Trade-Other" }, ViewOptions = new List<string> { "All", "Dealer", "Customer", "Region" }, DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange,view" },
        new() { Id = 2, Name = "Payment Collection", ShowView = true, ShowType = true, ShowAreas = true, ShowDealers = true, ShowRegions = true, TypeOptions = new List<string> { "None", "Ecomile", "Retread", "All" }, ViewOptions = new List<string> { "All", "Dealer", "Customer", "Region" }, DatePreset = "thisWeek,lastWeek,thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 3, Name = "Sales (Retd. & Ecomile)", ShowView = true, ShowType = true, ShowAreas = true, ShowDealers = true, ShowRegions = true, TypeOptions = new List<string> { "Sold", "Claim", "Rejected", "Intercompany" }, ViewOptions = new List<string> { "All", "Dealer", "Customer", "Area", "Region" }, DatePreset = "thisWeek,lastWeek,thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 4, Name = "Sales (Below Base Price)", DatePreset = "thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 5, Name = "Tyre Stock List", ShowType = true, TypeOptions = new List<string> { "Casing", "Prod Not Invoice", "Exchange Scrap", "Rejected Casing" }, DatePreset = "thisMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 6, Name = "Customer Trial Balance", ShowView = true, ShowType = true, ShowAreas = true, ShowDealers = true, ShowRegions = true, TypeOptions = new List<string> { "None", "Ecomile", "Retread", "All" }, ViewOptions = new List<string> { "Only Active", "Only Has Balance", "Show All" }, DatePreset = "thisWeek,lastWeek,thisMonth,lastMonth", OutputFormats = "pdf,excel" },
        new() { Id = 7, Name = "Statement of Account", ShowType = true, ShowCustomers = true, ShowDealers = true, TypeOptions = new List<string> { "Show Dealer Name", "Hide Dealer Name" }, DatePreset = "thisWeek,lastWeek,thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange,customers" },
        new() { Id = 8, Name = "Posted Sales Invoice", OutputFormats = "pdf" },
        new() { Id = 9, Name = "Posted Sales CreditMemo", OutputFormats = "pdf" },
        new() { Id = 10, Name = "Posted Claim Form", OutputFormats = "pdf" },
        new() { Id = 11, Name = "Ecomile Item Sold", OutputFormats = "pdf,excel" },
        new() { Id = 12, Name = "Day Transaction", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 13, Name = "Product Mix (Ecomile)", DatePreset = "thisWeek,thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
        new() { Id = 14, Name = "Size / Make (Ecomile)", DatePreset = "thisWeek,thisMonth,lastMonth", OutputFormats = "pdf,excel", RequiredFields = "dateRange" },
    };

    /// <inheritdoc />
    public async Task<List<ReportMeta>> GetReportMetaAsync(
        ITenantScope scope,
        string? reports = null,
        CancellationToken cancellationToken = default)
    {
        var query = scope.Query<GroupDetails>()
            .Where(x => x.Category == "RPT-SALES");

        var detailsArr = await scope.ToArrayAsync(query.OrderBy(x => x.Name), cancellationToken).ConfigureAwait(false);

        // Filter by report codes in memory (query layer does not support names.Contains(x.Code) in all contexts)
        if (!string.IsNullOrWhiteSpace(reports))
        {
            var names = new HashSet<string>(reports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
            if (names.Count > 0 && detailsArr != null)
                detailsArr = detailsArr.Where(d => names.Contains(d.Code)).ToArray();
        }
        var meta = ReportMetaList;
        var result = new List<ReportMeta>();

        foreach (var detail in detailsArr)
        {
            var m = meta.FirstOrDefault(x => string.Equals(x.Name, detail.Name, StringComparison.OrdinalIgnoreCase));
            if (m != null)
            {
                result.Add(new ReportMeta
                {
                    Id = m.Id,
                    Code = detail.Code,
                    Name = m.Name,
                    DatePreset = m.DatePreset,
                    OutputFormats = m.OutputFormats,
                    TypeOptions = m.TypeOptions,
                    ViewOptions = m.ViewOptions,
                    ShowType = m.ShowType,
                    ShowView = m.ShowView,
                    ShowCustomers = m.ShowCustomers,
                    ShowDealers = m.ShowDealers,
                    ShowAreas = m.ShowAreas,
                    ShowRegions = m.ShowRegions,
                    ShowRespCenters = m.ShowRespCenters,
                    ShowNos = m.ShowNos,
                    RequiredFields = m.RequiredFields
                });
            }
            else
            {
                // Fallback for reports not in hardcoded list
                result.Add(new ReportMeta { Id = 0, Code = detail.Code, Name = detail.Name, DatePreset = "Today" });
            }
        }

        return result;
    }

    private static Dictionary<string, object?>? BuildParameters(SalesReportParams p)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(p.From)) dict["From"] = p.From;
        if (!string.IsNullOrEmpty(p.To)) dict["To"] = p.To;
        return dict.Count > 0 ? dict : null;
    }

    /// <summary>
    /// Fetches the RDLC data for a report. 
    /// Updated to ensure document-specific reports (Invoice, Credit Memo, Claim) handle multiple Nos for batch printing.
    /// </summary>
    private async Task<(string rdlcName, object? data, Dictionary<string, object?>? reportParameters)> GetReportDataAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams parameters,
        CancellationToken cancellationToken)
    {
        return reportName.Trim() switch
        {
            "Posted Sales Invoice" => WithDefaultParams(await GetPostedSalesInvoiceAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Posted Sales CreditMemo" => WithDefaultParams(await GetPostedSalesCreditMemoAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Posted Claim Form" => WithDefaultParams(await GetPostedClaimFormAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Sales & Balance" => WithDefaultParams(await GetSalesAndBalanceAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Payment Collection" => WithDefaultParams(await GetPaymentCollectionAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Sales (Retd. & Ecomile)" => WithDefaultParams(await GetSalesTyreAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Sales (Below Base Price)" => WithDefaultParams(await GetSalesBelowBasePriceAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Tyre Stock List" => WithDefaultParams(await GetEcomileTyreStockAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Customer Trial Balance" => await GetCustomerTrialBalanceAsync(scope, parameters, cancellationToken).ConfigureAwait(false),
            "Statement of Account" => await GetStatementOfAccountAsync(scope, parameters, cancellationToken).ConfigureAwait(false),
            "Ecomile Item Sold" => WithDefaultParams(await GetEcomileItemSoldAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Day Transaction" => WithDefaultParams(await GetDayTransactionAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Product Mix (Ecomile)" => WithDefaultParams(await GetProductMixEcomileAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            "Size / Make (Ecomile)" => WithDefaultParams(await GetEcomileTyreMakeAsync(scope, parameters, cancellationToken).ConfigureAwait(false)),
            _ => ("", null, null)
        };
    }

    private static (string rdlcName, object? data, Dictionary<string, object?>? reportParameters) WithDefaultParams((string rdlcName, object? data) t) => (t.rdlcName, t.data, null);

    private async Task<(string rdlcName, object? data)> GetPostedSalesInvoiceAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        string lineT = scope.GetQualifiedTableName("Sales Invoice Line", isShared: false);
        string headerT = scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);
        string customerT = scope.GetQualifiedTableName("Customer", isShared: false);
        string itemT = scope.GetQualifiedTableName("Item", isShared: false);
        string uomT = scope.GetQualifiedTableName("Unit of Measure", isShared: false);
        string itemVariantT = scope.GetQualifiedTableName("Item Variant", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string teamT = scope.GetQualifiedTableName("Team Salesperson", isShared: false);
        string territoryT = scope.GetQualifiedTableName("Territory", isShared: false);
        string userT = scope.GetQualifiedTableName("User", isShared: true);

        if (p.Nos == null || p.Nos.Count == 0) return ("PostedSalesInvoice", null);

        string nosIn = string.Join(",", p.Nos.Select((_, i) => $"@n{i}"));
        var parameters = new Dictionary<string, object?>();
        for (int i = 0; i < p.Nos.Count; i++) parameters[$"n{i}"] = p.Nos[i];

        string Decimal0 = "#,0;(#,0);'-'";
        string Decimal1 = "#,0.0;(#,0.0);'-'";
        string Decimal2 = "#,0.00;(#,0.00);'-'";

        string sql = $@"
        SELECT 
            Line.[Line No_] as [LineNo], Line.Quantity,  Line.Description as Line_Description,
            Line.Make, Line.Pattern, Line.[Serial No] as SerialNo, Line.No_ as Line_No_,
            UOM.Description as UOM, Line.[Total GST Amount] as Line_TotalGstAmount, Line.[GST Jurisdiction Type] as Line_GstJurisdictionType,
            Line.[Gen_ Prod_ Posting Group] as Line_GenProdPostGroup, 
            Line.[GST _] as Line_GstPercent, Line.[GST Base Amount] as Line_BaseGstAmount, Line.[Text on Invoice] as Remark,
            Line.Freight as Line_Frieght, Line.[TCS on Sales] as Line_TDS, Line.[Line Discount Amount] as Line_LineDiscAmount,
            Line.[HSN_SAC Code] as HSNSAC, Line.[Product Group Code] as Item_ProdGroup, Line.[Line Amount] as LineAmount,               
            Header.No_ as InvoiceNo, Header.[External Document No_] as ExtDocNo, Header.[P_O No_] as PONo,
            Header.[Sell-to Customer No_] as CustomerNo, Header.[E-Inv Ack No_] as EInvAckNo,
            Header.[E-Inv IRN No_] as EInvIRNNo, Header.[E-Inv Signed QRCode] as EinvQRCode, Header.[Posting Date] as [Date],
            Header.[Responsibility Center] as RespCenter,
            Item.Description as Item_Description, Item.[Alternative Item No_] as Item_AlternateNo,
            Item.[No_ 2] as Item_No2, Item.[Item Category Code] as Item_ItemCategory,
            ItemVariant.Pattern as ItemVariant_Pattern,
            Terr.[Bank Ac No] as BankAccNo,
            Line.[Unit Price] as Rate,
            ROUND(Line.[Amount To Customer], 0) as FinalAmount,
            Header.[Posting Date] as PostingDate,
            [User].[Full Name] as UserName,
            Customer.[Gen_ Bus_ Posting Group] as CustomerGenBusPostingGroup,
            (SELECT SUM(l.[Amount To Customer]) FROM {lineT} l WHERE l.[Document No_] = Line.[Document No_]) as GrandTotal,
            (SELECT SUM(l.[Amount To Customer]) FROM {lineT} l WHERE l.[Document No_] = Line.[Document No_] AND l.[No_] = '9400') as RoundUp,
            FORMAT(Header.[Posting Date], 'dd. MMMM yyyy') as InvoiceDate,
            IIF(Header.[E-Inv Ack Date] > '1753-01-01', FORMAT(Header.[E-Inv Ack Date], 'dd. MMMM yyyy'), '') as EInvAckDate,
            IIF(Customer.[Gen_ Bus_ Posting Group] <> 'SALES', 'Branch Transfer', 'Tax Invoice') as InvoiceText,
            IIF(Customer.[Gen_ Bus_ Posting Group] <> 'SALES', 'Br. Transfer', 'Invoice No.') as DocumentCaption
        FROM {lineT} as Line
        INNER JOIN {headerT} as Header ON Line.[Document No_] = Header.[No_]
        LEFT JOIN {customerT} as Customer ON Header.[Sell-to Customer No_] = Customer.No_
        LEFT JOIN {itemT} as Item ON Line.No_ = Item.No_
        LEFT JOIN {uomT} as UOM ON Line.[Unit of Measure Code] = UOM.Code
        LEFT JOIN {itemVariantT} as ItemVariant ON Line.No_ = ItemVariant.[Item No_] AND Line.[Variant Code] = ItemVariant.Code
        LEFT JOIN {areaT} as Area ON Customer.[Area Code] = Area.Code
        LEFT JOIN {teamT} as Team ON Area.Team = Team.[Team Code] AND Team.Type = 6
        LEFT JOIN {territoryT} as Terr ON Team.Code = Terr.Code
        LEFT JOIN {userT} as [User] ON Header.[User ID] = [User].[User Name]
        WHERE Line.[No_] <> '9400' AND Line.[Document No_] IN ({nosIn})";

        var dataRecs = (await scope.RawQueryToArrayAsync<PostedSalesInvoiceRow>(sql, parameters, ct).ConfigureAwait(false)).ToList();
        
        if (dataRecs.Count == 0) return ("PostedSalesInvoice", null);

        var bankAccounts = await GetBankAccountsAsync(scope, p, ct);
        var respCenters = await GetResponsibilityCentersAsync(scope, p, ct);
        var docAddresses = await GetDocumentAddressAsync(scope, "Invoice", p.Nos.ToArray(), ct);
        var gstComponents = await GetGstComponentsAsync(scope, ct);
        
        var igstComponent = gstComponents.FirstOrDefault(c => c.Code == "IGST");
        var sgstComponent = gstComponents.FirstOrDefault(c => c.Code == "CGST");
        var cgstComponent = gstComponents.FirstOrDefault(c => c.Code == "SGST");

        var records = new List<PostedSalesInvoiceRow>();
        string docNo = string.Empty;
        decimal sgstTotal = 0, cgstTotal = 0, igstTotal = 0, gstBaseTotal = 0, gstTotal = 0, frgtTotal = 0, tdsTotal = 0, discTotal = 0, finAmountTotal = 0, qtyTotal = 0;

        foreach (var record in dataRecs.OrderBy(c => c.InvoiceNo).ThenBy(c => c.LineNo))
        {
            if (docNo != record.InvoiceNo)
            {
                sgstTotal = 0; cgstTotal = 0; igstTotal = 0; gstBaseTotal = 0; gstTotal = 0;
                frgtTotal = 0; tdsTotal = 0; discTotal = 0; finAmountTotal = 0; qtyTotal = 0;
                docNo = record.InvoiceNo ?? "";
            }

            var respCenter = respCenters.FirstOrDefault(c => c.Code == record.RespCenter);
            var respCenterHO = respCenters.FirstOrDefault(c => c.Code == "HO");

            if (respCenter != null)
            {
                record.Company = respCenter.Company;
                record.Logo = respCenter.Logo;
                record.Company_Address = respCenter.GetAddressInSingleLine(true);
                record.Company_Address2 = respCenter.GetContactWebLine();
                record.PANText = respCenter.GetPANNo();
                record.GSTText = respCenter.GetGSTNo();
                record.CINText = respCenter.GetCINNo();
                record.JurisdictionText = respCenter.GetJurisdiction();
                if (respCenterHO != null)
                    record.RegOffAddress = respCenter.GetRegOfficeAddressInSingleLine(true, respCenterHO);

                System.Console.WriteLine(respCenter.NatureOfBusiness.ToString());
                switch (respCenter.NatureOfBusiness)
                {
                    case NatureOfBusiness.SalesDepot:
                    case NatureOfBusiness.TyreRetreading:
                        {
                            record.QuantityFormatTxt = Decimal0;
                            if (new[] { "RETD", "CASING", "ECOMILE" }.Contains(record.Item_ItemCategory))
                            {
                                if ((record.Make ?? "").Length > 3)
                                    record.Make = ToTitleCase(record.Make!);

                                string makeStr = record.Make ?? "";

                                if (!string.IsNullOrEmpty(record.Pattern))
                                    record.ItemDiscription = $"{record.Item_AlternateNo} {record.Pattern} - {record.SerialNo} {makeStr}";
                                else
                                {
                                    if (!string.IsNullOrEmpty(record.ItemVariant_Pattern))
                                        record.ItemDiscription = $"{record.Item_AlternateNo} {record.ItemVariant_Pattern} - {record.SerialNo} {makeStr}";
                                    else
                                        record.ItemDiscription = $"{record.Item_AlternateNo} - {record.SerialNo} {makeStr}";
                                }
                            }
                            else if (record.Item_ItemCategory == "TRADE")
                            {
                                record.ItemDiscription = record.Item_Description;
                            }
                            else if (record.Item_ItemCategory == "RAW-MAT")
                            {
                                if (!string.IsNullOrEmpty(record.Item_No2))
                                    record.ItemDiscription = ToTitleCase(record.Item_No2);
                                else
                                    record.ItemDiscription = record.Line_No_;
                            }
                            break;
                        }
                    case NatureOfBusiness.RubberManufacturing:
                    case NatureOfBusiness.TilesManufacturing:
                        {
                            record.QuantityFormatTxt = Decimal1;
                            if (respCenter.NatureOfBusiness == NatureOfBusiness.RubberManufacturing)
                            {
                                if (record.Item_ItemCategory == "FIN-GOODS")
                                {
                                    if (record.Line_No_ == "JOB WORK")
                                        record.ItemDiscription = record.Line_Description;
                                    else
                                        record.ItemDiscription = record.Item_Description;
                                }
                            }
                            else
                            {
                                record.ItemDiscription = record.Line_Description;
                            }
                            break;
                        }
                    default:
                        {
                            record.ItemDiscription = record.Line_Description;
                            break;
                        }
                }
                // Ensure BEL gets Decimal0 for quantity in report (premium formatting)
                if (string.Equals(respCenter.Code, "BEL", StringComparison.OrdinalIgnoreCase))
                    record.QuantityFormatTxt = Decimal0;
            }

            if (igstComponent != null) record.IGSTText = igstComponent.Value;
            if (sgstComponent != null) record.SGSTText = sgstComponent.Value;
            if (cgstComponent != null) record.CGSTText = cgstComponent.Value;

            var address = docAddresses.FirstOrDefault(c => c.No == record.InvoiceNo);
            if (address != null)
            {
                record.BillToAddress1 = address.BillToAddress[0];
                record.BillToAddress2 = address.BillToAddress[1];
                record.BillToAddress3 = address.BillToAddress[2];
                record.BillToAddress4 = address.BillToAddress[3];
                record.BillToAddress5 = address.BillToAddress[4];
                record.BillToAddress6 = address.BillToAddress[5];
                record.BillToAddress7 = address.BillToAddress[6];

                record.ShipToAddress1 = address.ShipToAddress[0];
                record.ShipToAddress2 = address.ShipToAddress[1];
                record.ShipToAddress3 = address.ShipToAddress[2];
                record.ShipToAddress4 = address.ShipToAddress[3];
                record.ShipToAddress5 = address.ShipToAddress[4];
                record.ShipToAddress6 = address.ShipToAddress[5];
                record.ShipToAddress7 = address.ShipToAddress[6];
            }

            var bankAcc = bankAccounts.FirstOrDefault(b => b.Code == record.BankAccNo);
            if (bankAcc != null)
            {
                record.BankText01 = "For NEFT/RTGS";
                record.BankText02 = $"Virtual A/c No. {bankAcc.AccountNo}";
                record.BankText03 = "For other payment method";
                record.BankText04 = $"A/c No. {bankAcc.RealBankAccountNo}";
                record.BankText05 = $"IFSC Code. {bankAcc.IFSCCode}";
            }

            if (!string.IsNullOrEmpty(record.PONo))
            {
                record.ExternalDocNo = record.PONo;
                record.ExternalDocNoCaption = "P.O No.";
            }
            else
            {
                record.ExternalDocNo = record.ExtDocNo;
                record.ExternalDocNoCaption = "C.I No.";
            }

            if (record.Line_TotalGstAmount > 0)
            {
                if (record.Line_GstJurisdictionType == 0)
                {
                    record.SGSTPercent = Math.Round(record.Line_GstPercent / 2, 2);
                    record.CGSTPercent = Math.Round(record.Line_GstPercent / 2, 2);
                    record.SGSTAmount = Math.Round(record.Line_TotalGstAmount / 2, 2);
                    record.CGSTAmount = Math.Round(record.Line_TotalGstAmount / 2, 2);
                }
                else
                {
                    record.IGSTPercent = Math.Round(record.Line_GstPercent, 2);
                    record.IGSTAmount = Math.Round(record.Line_TotalGstAmount, 2);
                }
                record.Line_GSTBaseAmount = record.Line_BaseGstAmount;
                record.Line_FinalAmount = record.FinalAmount;
                record.Line_SGSTAmount = record.SGSTAmount;
                record.Line_CGSTAmount = record.CGSTAmount;
                record.Line_IGSTAmount = record.IGSTAmount;
            }

            record.GSTPercentFormat = Decimal0;
            if (record.CGSTPercent == 2.5m) record.GSTPercentFormat = Decimal1;

            record.GSTPercentage = Math.Round(record.Line_GstPercent);
            cgstTotal += record.CGSTAmount;
            sgstTotal += record.SGSTAmount;
            igstTotal += record.IGSTAmount;
            gstTotal += record.Line_TotalGstAmount;
            gstBaseTotal += record.Line_BaseGstAmount;
            frgtTotal += record.Line_Frieght;
            tdsTotal += record.Line_TDS;
            discTotal += record.Line_LineDiscAmount;
            finAmountTotal += record.FinalAmount;
            if (record.Item_ProdGroup != "PATCHES") qtyTotal += record.Quantity;

            record.SGSTAmount = sgstTotal;
            record.CGSTAmount = cgstTotal;
            record.IGSTAmount = igstTotal;
            record.TotalFinalAmount = finAmountTotal;
            record.DiscountAmount = record.Line_LineDiscAmount;
            record.BaseAmount = gstBaseTotal;
            record.GstAmount = gstTotal;
            record.TotalQty = qtyTotal;
            record.GrandTotalText = ToWords(Math.Round(record.GrandTotal, 1), "(INR) ", "paisa only.");

            if (!(record.Item_ProdGroup == "PATCHES" && record.Line_GenProdPostGroup == "ECOMILE"))
                records.Add(record);
        }

        if (records.Count > 0)
            records = records.OrderByDescending(c => c.Date).ThenBy(c => c.InvoiceNo).ThenBy(c => c.LineNo).ToList();

        return ("PostedSalesInvoice", ToDataTable(records));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, (System.Reflection.PropertyInfo[] Props, Type[] ColTypes)> ToDataTableSchemaCache = new();

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
                types[i] = propType == typeof(DateTime) ? typeof(DateTime) : propType == typeof(decimal) ? typeof(decimal) : propType == typeof(int) ? typeof(int) : propType == typeof(bool) ? typeof(bool) : propType == typeof(byte[]) ? typeof(byte[]) : typeof(string);
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

    private async Task<(string rdlcName, object? data)> GetPostedSalesCreditMemoAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        string lineT = scope.GetQualifiedTableName("Sales Cr_Memo Line", isShared: false);
        string headerT = scope.GetQualifiedTableName("Sales Cr_Memo Header", isShared: false);
        string customerT = scope.GetQualifiedTableName("Customer", isShared: false);
        string itemT = scope.GetQualifiedTableName("Item", isShared: false);
        string uomT = scope.GetQualifiedTableName("Unit of Measure", isShared: false);
        string itemVariantT = scope.GetQualifiedTableName("Item Variant", isShared: false);
        string userT = scope.GetQualifiedTableName("User", isShared: true);

        if (p.Nos == null || p.Nos.Count == 0) return ("PostedSalesCreditMemo", null);

        string nosIn = string.Join(",", p.Nos.Select((_, i) => $"@n{i}"));
        var parameters = new Dictionary<string, object?>();
        for (int i = 0; i < p.Nos.Count; i++) parameters[$"n{i}"] = p.Nos[i];

        string sql = $@"
        SELECT 
            Line.[Line No_] as [LineNo], Line.Quantity, Line.Description as Description,
            Line.Make, Line.Pattern, Line.[Serial No] as SerialNo, Line.No_ as Line_No_,
            UOM.Description as UOM, Line.[Total GST Amount] as Line_TotalGstAmount, Line.[GST Jurisdiction Type] as Line_GstJurisdictionType,
            Line.[Gen_ Prod_ Posting Group] as Line_GenProdPostGroup, Line.[Unit Price] as UnitPrice, 
            Line.[Line Amount] as LineAmount,
            Line.[GST _] as Line_GstPercent, Line.[GST Base Amount] as gstBaseAmount, Line.[Text on Invoice] as Remark,
            Line.Freight as Line_Frieght, Line.[TCS on Sales] as Line_TDS, Line.[Line Discount Amount] as Line_LineDiscAmount,
            Line.[HSN_SAC Code] as HSNSAC, Line.[Product Group Code] as Item_ProdGroup,
            LineTotal.GrTotal, LineRndup.roundUp,
            Header.No_ as No_SalesCrMemoHeader, Header.[External Document No_] as ExtDocNo, Header.[P_O No_] as PONo,
            Header.[Sell-to Customer No_] as SelltoCustomerNo_SalesCrMemoHeader, Header.[E-Inv Ack No_] as EInvAckNo,
            Header.[E-Inv IRN No_] as EInvIRNNo, Header.[E-Inv Signed QRCode] as QRCode, Header.[Posting Date] as [Date],
            Format(Header.[Posting Date], 'dd. MMMM yyyy') as PostingDate_SalesCrMemoHeader,
            Header.[Responsibility Center] as ResponsibilityCenter,
            Item.Description as Item_Description, Item.[Alternative Item No_] as Item_AlternateNo,
            Item.[No_ 2] as Item_No2, Item.[Item Category Code] as Item_ItemCategory,
            ItemVariant.Pattern as ItemVariant_Pattern,
            Line.[Unit Price] as Rate,
            ROUND(Line.[Amount To Customer], 0) as FinalAmount,
            Header.[Posting Date] as PostingDate,
            [User].[Full Name] as UserName,
            Customer.[Gen_ Bus_ Posting Group] as CustomerGenBusPostingGroup
        FROM {lineT} as Line
        INNER JOIN {headerT} as Header ON Line.[Document No_] = Header.[No_]
        LEFT JOIN {customerT} as Customer ON Header.[Sell-to Customer No_] = Customer.No_
        LEFT JOIN {itemT} as Item ON Line.No_ = Item.No_
        LEFT JOIN {uomT} as UOM ON Line.[Unit of Measure Code] = UOM.Code
        LEFT JOIN {itemVariantT} as ItemVariant ON Line.No_ = ItemVariant.[Item No_] AND Line.[Variant Code] = ItemVariant.Code
        LEFT JOIN {userT} as [User] ON Header.[User ID] = [User].[User Name]
        LEFT JOIN (SELECT SUM([Amount To Customer]) as GrTotal, [Document No_] as DocumentNo FROM {lineT} GROUP BY [Document No_]) as LineTotal ON LineTotal.DocumentNo = Line.[Document No_]
        LEFT JOIN (SELECT SUM([Amount To Customer]) as roundUp, [Document No_] as DocumentNo FROM {lineT} WHERE No_ = '9400' GROUP BY [Document No_]) AS LineRndup ON LineRndup.DocumentNo = Line.[Document No_]
        WHERE Line.[Document No_] IN ({nosIn})";

        var records = (await scope.RawQueryToArrayAsync<PostedSalesCreditMemoRow>(sql, parameters, ct).ConfigureAwait(false)).ToList();
        
        if (records.Count == 0) return ("PostedSalesCreditMemo", null);

        var respCenters = await GetResponsibilityCentersAsync(scope, p, ct);
        var resRegOffice = respCenters.FirstOrDefault(c => c.Code == "HO");
        var documentAddress = await GetDocumentAddressAsync(scope, "CreditMemo", p.Nos.ToArray(), ct);

        foreach (var row in records)
        {
            var res = respCenters.FirstOrDefault(c => c.Code == row.ResponsibilityCenter) ?? resRegOffice;
            var address = documentAddress.FirstOrDefault(a => a.No == row.No_SalesCrMemoHeader);

            if (res != null)
            {
                row.COMPANYNAME = res.Company;
                row.Logo = res.Logo;
                row.CompAddress = res.GetAddressInSingleLine(true);
                row.CompAddress2 = res.GetContactWebLine();
                row.RegOffAddress = resRegOffice != null ? resRegOffice.GetRegOfficeAddressInSingleLine(true, resRegOffice) : "";
                row.PANText = res.GetPANNo();
                row.GSTText = res.GetGSTNo();
                row.CINText = res.GetCINNo();
                row.JuriText = res.GetJurisdiction();
            }

            if (address != null)
            {
                row.BillToAddress1 = address.BillToAddress[0];
                row.BillToAddress2 = address.BillToAddress[1];
                row.BillToAddress3 = address.BillToAddress[2];
                row.BillToAddress4 = address.BillToAddress[3];
                row.BillToAddress5 = address.BillToAddress[4];
                row.BillToAddress6 = address.BillToAddress[5];

                row.SellToAddress1 = address.ShipToAddress[0];
                row.SellToAddress2 = address.ShipToAddress[1];
                row.SellToAddress3 = address.ShipToAddress[2];
                row.SellToAddress4 = address.ShipToAddress[3];
                row.SellToAddress5 = address.ShipToAddress[4];
                row.SellToAddress6 = address.ShipToAddress[5];
            }

            if (row.Line_TotalGstAmount > 0)
            {
                if(row.Line_GstJurisdictionType == 0)
                {
                    row.sGstPer = Math.Round(row.Line_GstPercent / 2);
                    row.cGstPer = Math.Round(row.Line_GstPercent / 2);
                    row.sGstAmt = Math.Round(row.Line_TotalGstAmount / 2, 2);
                    row.cGstAmt = Math.Round(row.Line_TotalGstAmount / 2, 2);
                }
                else
                {
                    row.iGstPer = Math.Round(row.Line_GstPercent, 2);
                    row.iGstAmt = Math.Round(row.Line_TotalGstAmount, 2);
                }
            }
        }

        records = records.OrderBy(record => record.No_SalesCrMemoHeader).ThenBy(rec => rec.LineNo).ToList();
        var invoices = records.GroupBy(c => c.No_SalesCrMemoHeader).Select(c => c.First()).ToList();
        foreach (var inv in invoices)
        {
            var grouped = records.Where(c => c.No_SalesCrMemoHeader == inv.No_SalesCrMemoHeader).ToList();
            var nonZeroRec = records.Where(c => c.No_SalesCrMemoHeader == inv.No_SalesCrMemoHeader && (c.cGstAmt > 0 || c.iGstAmt > 0)).First();
            decimal gstBaseAmt = grouped.Sum(c => c.gstBaseAmount);
            decimal discAmt = grouped.Sum(c => c.discAmt);
            decimal cGstAmt = grouped.Sum(c => c.cGstAmt);
            decimal iGstAmt = grouped.Sum(c => c.iGstAmt);
            decimal cGstPer = 0;
            decimal iGstPer = 0;
            if (nonZeroRec != null)
            {
                if (nonZeroRec.cGstPer != 0)
                    cGstPer = nonZeroRec.cGstPer;
                if (nonZeroRec.iGstPer != 0)
                    iGstPer = nonZeroRec.iGstPer;
            }
            foreach (var rec in grouped)
            {

                rec.gstBaseAmount = gstBaseAmt;
                rec.discAmt = discAmt;
                rec.cGstAmt = cGstAmt;
                rec.sGstAmt = cGstAmt;
                rec.iGstAmt = iGstAmt;
                rec.iGstPer = iGstPer;
                rec.cGstPer = cGstPer;
                rec.sGstPer = cGstPer;

                rec.showCGST = !(cGstAmt > 0);
                rec.showIGST = !(iGstAmt > 0);
                rec.showSGST = !(cGstAmt > 0);
            }
        }
        return ("PostedSalesCreditMemo", ToDataTable(records));
    }

    private async Task<(string rdlcName, object? data)> GetPostedClaimFormAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        string claimT = scope.GetQualifiedTableName("Claim & Failure Posted", isShared: false);
        string claimSettT = scope.GetQualifiedTableName("Claim & Failure Settlement", isShared: false);
        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);

        if (p.Nos == null || p.Nos.Count == 0) return ("PostedClaimForm", null);

        string nosIn = string.Join(",", p.Nos.Select((_, i) => $"@n{i}"));
        var parameters = new Dictionary<string, object?>();
        for (int i = 0; i < p.Nos.Count; i++) parameters[$"n{i}"] = p.Nos[i];

        string sql = $@"
        SELECT 
            Claim.[No_] as No_ClaimFailurePosted, 
            CAST(Claim.[Type] AS VARCHAR) as Type_ClaimFailurePosted, 
            Claim.[Customer No_] as CustomerNo_ClaimFailurePosted,
            Claim.[Name] as Name_ClaimFailurePosted,
            Cust.[Name] as CustomerName, 
            Claim.[Item No_] as ItemNo_ClaimFailurePosted, 
            Claim.[Serial No_] as SerialNo_ClaimFailurePosted,
            Claim.[Make] as Make_ClaimFailurePosted, 
            Claim.[Invoice No_] as InvoiceNo_ClaimFailurePosted, 
            Claim.[Run Period] as RunPeriod_ClaimFailurePosted,
            FORMAT(Claim.[Invoice Date], 'dd. MMMM yyyy') as InvoiceDate_ClaimFailurePosted,
            Format(Claim.[Posting Date], 'dd. MMMM yyyy') as PostingDate_ClaimFailurePosted,
            Claim.[Variant] as Variant_ClaimFailurePosted, 
            Claim.[Inspection Report] as InspectionReport_ClaimFailurePosted,
            Claim.[Owner Risk] as OwnerRisk_ClaimFailurePosted,
            Sett.[Compensation Amount] as CompensationAmount, 
            CASE Sett.[Decision] 
                WHEN 0 THEN ' ' 
                WHEN 1 THEN 'Retread' 
                WHEN 2 THEN 'Repair' 
                WHEN 3 THEN 'Reject' 
                WHEN 4 THEN 'Issue Credit Note' 
                WHEN 5 THEN 'Recomanded to use' 
                WHEN 6 THEN 'Casing Replaced' 
                WHEN 7 THEN 'Ecomile Replaced' 
                ELSE ' ' 
            END as Decision_ClaimFailureSettlement,
            Claim.[Responsibility Center] as RespCenter, 
            Claim.[Posting Date] as PostingDate,
            CASE Sett.[Sanction Type] 
                WHEN 0 THEN ' ' 
                WHEN 1 THEN 'Pro-rata' 
                WHEN 2 THEN 'Special-Case' 
                ELSE ' ' 
            END as SanctionType_ClaimFailureSettlement,
            Format(Sett.[Date], 'dd/MM/yy') as Date_ClaimFailureSettlement,
            Sett.[Percentage] as Percentage_ClaimFailureSettlement, 
            Sett.[Compensation Amount] as CompensationAmount_ClaimFailureSettlement,
            Sett.[Sales Unit Price] as SalesUnitPrice_ClaimFailureSettlement,
            Sett.[NSD] as NSD_ClaimFailureSettlement, 
            Sett.[Run Period] as RunPeriod_ClaimFailureSettlement, 
            Sett.[Fault Description] as FaultDescription_ClaimFailureSettlement,
            Area.[Name] as AreaName
        FROM {claimT} as Claim
        LEFT JOIN {claimSettT} as Sett ON Claim.[No_] = Sett.[Document No_]
        LEFT JOIN {custT} as Cust ON Claim.[Customer No_] = Cust.[No_]
        LEFT JOIN {areaT} as Area ON Cust.[Area Code] = Area.[Code]
        WHERE Claim.[No_] IN ({nosIn})";
        
        var records = (await scope.RawQueryToArrayAsync<PostedClaimFormRow>(sql, parameters, ct).ConfigureAwait(false)).ToList();
        
        if (records.Count == 0) return ("PostedClaimForm", null);

        var respCenters = await GetResponsibilityCentersAsync(scope, p, ct);
        var resRegOffice = respCenters.FirstOrDefault(c => c.Code == "HO");

        foreach (var row in records)
        {
            var res = respCenters.FirstOrDefault(c => c.Code == row.RespCenter) ?? resRegOffice;
            if (res != null)
            {
                row.CompanyName = res.Company;
                row.Logo = res.Logo;
                row.CompAddress = res.GetAddressInSingleLine(true);
                row.CompAddress2 = res.GetContactWebLine();
                row.RespName = res.Name;
            }
            row.ReportName = "Claim Form";
        }

        return ("PostedClaimForm", ToDataTable(records));
    }

    private async Task<(string rdlcName, object? data)> GetSalesAndBalanceAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string dealerT = scope.GetQualifiedTableName("Salesperson_Purchaser", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string teamsT = scope.GetQualifiedTableName("Team Salesperson", isShared: false);
        string glEntryT = scope.GetQualifiedTableName("G_L Entry", isShared: false);
        string detailedLedgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["period"] = $"For period : {fromDt:dd-MMM-yy} .. {toDt:dd-MMM-yy}",
            ["reportName"] = $"Sales & Balance ({p.View ?? "All"})"
        };

        bool hideCustomer = false, hideRegion = false, hideDealer = false;
        switch ((p.View ?? "").ToLowerInvariant())
        {
            case "customer": hideRegion = true; hideDealer = true; break;
            case "dealer": hideCustomer = true; hideRegion = true; break;
            case "region": hideCustomer = true; hideDealer = true; break;
            default: break;
        }

        param["hideCustomer"] = hideCustomer;
        param["hideRegion"] = hideRegion;
        param["hideDealer"] = hideDealer;

        string[] accounts1 = Array.Empty<string>();
        string[] accounts2 = Array.Empty<string>();
        string netSaleName = "", netSaleName2 = "";

        if (string.Equals(p.Type, "retread-ecomile", StringComparison.OrdinalIgnoreCase))
        {
            accounts1 = GetGLAccountForSale(SaleType.Retread).ToArray();
            accounts2 = GetGLAccountForSale(SaleType.Ecomile).ToArray();
            netSaleName = "Retread";
            netSaleName2 = "Ecomile";
        }
        else if (string.Equals(p.Type, "trade-other", StringComparison.OrdinalIgnoreCase))
        {
            if (p.RespCenters?.Contains("ECO-B") == true || p.RespCenters?.Contains("ECO-M") == true)
            {
                accounts1 = GetGLAccountForSale(SaleType.Ecoflex).ToArray();
                accounts2 = GetGLAccountForSale(SaleType.IcEcoflex).ToArray();
                netSaleName = "Sales & Service";
                netSaleName2 = "Inter company";
            }
            else
            {
                accounts1 = GetGLAccountForSale(SaleType.FlapTube).ToArray();
                accounts2 = GetGLAccountForSale(SaleType.Scrap).ToArray();
                netSaleName = "Trade (Tube/Flap)";
                netSaleName2 = "Other (Scrap)";
            }
        }

        param["accounts1"] = accounts1.Length > 0 ? accounts1 : new[] { "-1" };
        param["accounts2"] = accounts2.Length > 0 ? accounts2 : new[] { "-1" };
        param["netSaleName"] = netSaleName;
        param["netSaleName2"] = netSaleName2;

        var where = new List<string> { "1=1" };

        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add("Cust.[Responsibility Center] IN @respCenters");
        }
        if (p.Dealers?.Count > 0)
        {
            param["dealers"] = p.Dealers;
            where.Add("Cust.[Dealer Code] IN @dealers");
            param["filterText"] = "Dealers: " + string.Join(",", p.Dealers);
        }
        if (p.Areas?.Count > 0)
        {
            param["areas"] = p.Areas;
            where.Add("Cust.[Area Code] IN @areas");
            if (!param.ContainsKey("filterText")) param["filterText"] = "Areas: " + string.Join(",", p.Areas);
        }
        else if (p.Regions?.Count > 0)
        {
            param["regions"] = p.Regions;
            where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamsT} T ON A.[Team]=T.[Team Code] WHERE T.[Type]=6 AND T.[Code] IN @regions)");
            if (!param.ContainsKey("filterText")) param["filterText"] = "Regions: " + string.Join(",", p.Regions);
        }

        string filterTextObj = param.TryGetValue("filterText", out var ft) ? $"Filter : {ft}" : "";
        param["filterText"] = filterTextObj;

        // Apply EntityType Filters
        string entityType = (p.EntityType ?? "").Trim();
        if (!string.IsNullOrEmpty(p.EntityCode) && !string.IsNullOrEmpty(entityType))
        {
            if (string.Equals(entityType, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                param["userCode"] = p.EntityCode;
                where.Add("Cust.[No_] = @userCode");
            }
            else if (string.Equals(entityType, "Partner", StringComparison.OrdinalIgnoreCase))
            {
                param["userCode"] = p.EntityCode;
                where.Add("Cust.[Dealer Code] = @userCode");
            }
            else if (string.Equals(entityType, "PartnerGroup", StringComparison.OrdinalIgnoreCase))
            {
                param["userCode"] = p.EntityCode;
                where.Add($"Cust.[Dealer Code] IN (SELECT A.[Code] FROM {dealerT} A WHERE A.[Group] = @userCode)");
            }
            else if (string.Equals(entityType, "Employee", StringComparison.OrdinalIgnoreCase) && string.Equals(p.EntityDepartment, "Sales", StringComparison.OrdinalIgnoreCase))
            {
                param["userCode"] = p.EntityCode;
                where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamsT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode)");
            }
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
        SELECT 
            Cust.[Name] AS CustomerName,
            Dealer.[Code] AS DealerNo,
            Dealer.[Dealership Name] AS DealerName,
            Area.[Name] AS AreaName,
            Teams.[Code] AS RegionCode,
            Teams.[Name] AS RegionName,
            REPLACE(REPLACE(LEFT(Cust.[No_],3), 'CU','00'), 'C', '0')+RIGHT(Cust.[No_],5) AS CustomerNo,
            -ISNULL(GLE.Amount, 0) AS NetSale,
            -ISNULL(GLE2.Amount, 0) AS NetSale2,
            -(ISNULL(GLE.Amount, 0) + ISNULL(GLE2.Amount, 0)) AS TotalSale,
            ISNULL(Ledger.Amount, 0) AS Balance,
            @reportName AS ReportName,
            @period AS Period,
            @netSaleName AS NetSaleName,
            @netSaleName2 AS NetSaleName2,
            CAST(@hideCustomer AS BIT) AS HideCustomer,
            CAST(@hideRegion AS BIT) AS HideRegion,
            CAST(@hideDealer AS BIT) AS HideDealer,
            @filterText AS Filter
        FROM {custT} Cust
        LEFT JOIN {dealerT} Dealer ON Dealer.[Code] = Cust.[Dealer Code]
        LEFT JOIN {areaT} Area ON Area.[Code] = Cust.[Area Code]
        LEFT JOIN {teamsT} Teams ON Teams.[Team Code] = Area.[Team] AND Teams.[Type] = 6
        LEFT JOIN (
            SELECT [Source No_] AS SourceNo, SUM([Amount]) AS Amount
            FROM {glEntryT}
            WHERE [Posting Date] >= @fromDt AND [Posting Date] <= @toDt AND [G_L Account No_] IN @accounts1
            GROUP BY [Source No_]
        ) GLE ON GLE.SourceNo = Cust.[No_]
        LEFT JOIN (
            SELECT [Source No_] AS SourceNo, SUM([Amount]) AS Amount
            FROM {glEntryT}
            WHERE [Posting Date] >= @fromDt AND [Posting Date] <= @toDt AND [G_L Account No_] IN @accounts2
            GROUP BY [Source No_]
        ) GLE2 ON GLE2.SourceNo = Cust.[No_]
        LEFT JOIN (
            SELECT [Customer No_] AS CustomerNo, SUM([Amount]) AS Amount
            FROM {detailedLedgerT}
            WHERE [Posting Date] <= @toDt
            GROUP BY [Customer No_]
        ) Ledger ON Ledger.CustomerNo = Cust.[No_]
        {whereClause}
        ";

        var records = await scope.RawQueryToArrayAsync<SalesAndBalanceRow>(sql, param, ct).ConfigureAwait(false);
        var rows = records.Where(r => r.NetSale != 0 || r.NetSale2 != 0 || r.Balance != 0).ToList();

        if (string.Equals(p.View, "Dealer", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.GroupBy(r => r.DealerNo).Select(g => new SalesAndBalanceRow
            {
                ReportName = g.First().ReportName,
                HideCustomer = g.First().HideCustomer,
                HideDealer = g.First().HideDealer,
                HideRegion = g.First().HideRegion,
                NetSaleName = g.First().NetSaleName,
                NetSaleName2 = g.First().NetSaleName2,
                Period = g.First().Period,
                DealerNo = g.Key,
                DealerName = g.First().DealerName,
                NetSale = g.Sum(c => c.NetSale),
                NetSale2 = g.Sum(c => c.NetSale2),
                TotalSale = g.Sum(c => c.TotalSale),
                Balance = g.Sum(c => c.Balance),
                Filter = g.First().Filter
            }).ToList();
        }

        return ("SalesAndBalance", rows.Count > 0 ? ToDataTable(rows) : null);
    }

    private async Task<(string rdlcName, object? data)> GetPaymentCollectionAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string dealerT = scope.GetQualifiedTableName("Salesperson_Purchaser", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string teamsT = scope.GetQualifiedTableName("Team Salesperson", isShared: false);
        string detailedLedgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["period"] = $"For period : {fromDt:dd-MMM-yy} .. {toDt:dd-MMM-yy}",
            ["reportName"] = "Payment Collection"
        };

        bool hideCustomer = false, hideRegion = false, hideDealer = false, hideArea = false;
        switch ((p.View ?? "").ToLowerInvariant())
        {
            case "customer": hideRegion = true; hideDealer = true; hideArea = true; break;
            case "dealer": hideCustomer = true; hideRegion = true; hideArea = true; break;
            case "area": hideCustomer = true; hideDealer = true; hideRegion = true; hideArea = false; break;
            case "region": hideCustomer = true; hideDealer = true; hideRegion = false; hideArea = true; break;
            default: break;
        }

        param["hideCustomer"] = hideCustomer;
        param["hideRegion"] = hideRegion;
        param["hideDealer"] = hideDealer;
        param["hideArea"] = hideArea;

        int? dealerProduct = (p.Type ?? "all").ToLowerInvariant() switch
        {
            "none" => 0,
            "ecomile" => 1,
            "retread" => 2,
            _ => null
        };

        var where = new List<string> { "1=1" };

        if (dealerProduct.HasValue)
        {
            param["dealerProduct"] = dealerProduct.Value;
            where.Add("Dealer.[Product] = @dealerProduct");
        }

        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add("Cust.[Responsibility Center] IN @respCenters");
        }
        if (p.Dealers?.Count > 0)
        {
            param["dealers"] = p.Dealers;
            where.Add("Cust.[Dealer Code] IN @dealers");
            param["filterText"] = "Dealers: " + string.Join(",", p.Dealers);
        }
        if (p.Areas?.Count > 0)
        {
            param["areas"] = p.Areas;
            where.Add("Cust.[Area Code] IN @areas");
            if (!param.ContainsKey("filterText")) param["filterText"] = "Areas: " + string.Join(",", p.Areas);
        }
        else if (p.Regions?.Count > 0)
        {
            param["regions"] = p.Regions;
            where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamsT} T ON A.[Team]=T.[Team Code] WHERE T.[Type]=6 AND T.[Code] IN @regions)");
            if (!param.ContainsKey("filterText")) param["filterText"] = "Regions: " + string.Join(",", p.Regions);
        }

        string filterTextObj = param.TryGetValue("filterText", out var ft) ? $"Filter : {ft}" : "";
        param["filterText"] = filterTextObj;

        string entityType = (p.EntityType ?? "").Trim();
        if (!string.IsNullOrEmpty(p.EntityCode) && !string.IsNullOrEmpty(entityType))
        {
            param["userCode"] = p.EntityCode;
            if (string.Equals(entityType, "Customer", StringComparison.OrdinalIgnoreCase))
                where.Add("Cust.[No_] = @userCode");
            else if (string.Equals(entityType, "Partner", StringComparison.OrdinalIgnoreCase))
                where.Add("Cust.[Dealer Code] = @userCode");
            else if (string.Equals(entityType, "Employee", StringComparison.OrdinalIgnoreCase) && string.Equals(p.EntityDepartment, "Sales", StringComparison.OrdinalIgnoreCase))
                where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamsT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode)");
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
        SELECT 
            Cust.[Name] AS CustomerName,
            Dealer.[Code] AS DealerNo,
            Dealer.[Dealership Name] AS DealerName,
            Area.[Name] AS AreaName,
            Teams.[Code] AS RegionCode,
            Teams.[Name] AS RegionName,
            REPLACE(REPLACE(LEFT(Cust.[No_],3), 'CU','00'), 'C', '0')+RIGHT(Cust.[No_],5) AS CustomerNo,
            -ISNULL(Ledger.Amount, 0) AS Collection,
            @reportName AS ReportName,
            @period AS Period,
            CAST(@hideCustomer AS BIT) AS HideCustomer,
            CAST(@hideRegion AS BIT) AS HideRegion,
            CAST(@hideDealer AS BIT) AS HideDealer,
            CAST(@hideArea AS BIT) AS HideArea,
            @filterText AS Filter
        FROM {custT} Cust
        LEFT JOIN {dealerT} Dealer ON Dealer.[Code] = Cust.[Dealer Code]
        LEFT JOIN {areaT} Area ON Area.[Code] = Cust.[Area Code]
        LEFT JOIN {teamsT} Teams ON Teams.[Team Code] = Area.[Team] AND Teams.[Type] = 6
        INNER JOIN (
            SELECT [Customer No_] AS CustomerNo, SUM([Amount]) AS Amount
            FROM {detailedLedgerT}
            WHERE [Posting Date] >= @fromDt AND [Posting Date] <= @toDt AND [Document Type] IN (1, 6) AND [Journal Batch Name] <> ''
            GROUP BY [Customer No_]
        ) Ledger ON Ledger.CustomerNo = Cust.[No_]
        {whereClause}
        ";

        var records = await scope.RawQueryToArrayAsync<PaymentCollectionRow>(sql, param, ct).ConfigureAwait(false);
        var rows = records.Where(r => r.Collection != 0).ToList();

        if (string.Equals(p.View, "Dealer", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.GroupBy(r => r.DealerNo).Select(g => new PaymentCollectionRow
            {
                ReportName = g.First().ReportName,
                HideCustomer = g.First().HideCustomer,
                HideDealer = g.First().HideDealer,
                HideRegion = g.First().HideRegion,
                HideArea = g.First().HideArea,
                Period = g.First().Period,
                DealerNo = g.Key,
                DealerName = g.First().DealerName,
                Collection = g.Sum(c => c.Collection),
                Filter = g.First().Filter
            }).ToList();
        }

        return ("PaymentCollection", rows.Count > 0 ? ToDataTable(rows) : null);
    }

    private async Task<(string rdlcName, object? data)> GetSalesTyreAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string dealerT = scope.GetQualifiedTableName("Salesperson_Purchaser", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string teamsT = scope.GetQualifiedTableName("Team Salesperson", isShared: false);
        string glEntryT = scope.GetQualifiedTableName("G_L Entry", isShared: false);
        string itemLedgerT = scope.GetQualifiedTableName("Item Ledger Entry", isShared: false);
        string locT = scope.GetQualifiedTableName("Location", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["period"] = $"For period : {fromDt:dd-MMM-yy} .. {toDt:dd-MMM-yy}"
        };

        string type = (p.Type ?? "").Trim().ToLowerInvariant();
        string reportName = $"Sales (Retd. & Ecomile) [ {p.Type} ] [ {p.View} ]";
        param["reportName"] = reportName;

        bool hideCustomer = false, hideRegion = false, hideDealer = false, hideArea = false;
        switch ((p.View ?? "").ToLowerInvariant())
        {
            case "customer": hideRegion = true; hideDealer = true; hideArea = true; break;
            case "dealer": hideCustomer = true; hideRegion = true; hideArea = true; break;
            case "area": hideCustomer = true; hideDealer = true; hideRegion = true; hideArea = false; break;
            case "region": hideCustomer = true; hideDealer = true; hideRegion = false; hideArea = true; break;
            default: break;
        }

        param["hideCustomer"] = hideCustomer;
        param["hideRegion"] = hideRegion;
        param["hideDealer"] = hideDealer;
        param["hideArea"] = hideArea;

        int[] locTypes;
        string product01 = "RETREAD", product02 = "ECOMILE";
        string custGenBusGroupCrit = "";
        string[] glAccounts = Array.Empty<string>();
        string[] glAccounts2 = Array.Empty<string>();
        string[] retreadCats = new[] { "RETD" };
        string[] ecomileCats = new[] { "ECOMILE" };

        switch (type)
        {
            case "sold":
                locTypes = new[] { 0, 1, 3 };
                custGenBusGroupCrit = "Cust.[Gen_ Bus_ Posting Group] = 'SALES'";
                glAccounts = GetGLAccountForSale(SaleType.Retread).ToArray();// new[] { "3110", "7572" }; // Retread
                glAccounts2 = GetGLAccountForSale(SaleType.Ecomile).ToArray(); //  new[] { "3126", "7573" }; // Ecomile
                break;
            case "claim":
                locTypes = new[] { 5 };
                break;
            case "rejected":
                locTypes = new[] { 2 };                
                break;
            case "intercompany":
                locTypes = new[] { 0, 1, 3, 4 };
                product01 = "RETD/CASING";
                custGenBusGroupCrit = "Cust.[Gen_ Bus_ Posting Group] <> 'SALES'";
                glAccounts = GetGLAccountForSale(SaleType.IcTyre).ToArray(); //new[] { "3302", "3303" }; // IcTyre
                glAccounts2 = GetGLAccountForSale(SaleType.IcTyre).ToArray(); //new[] { "3302", "3303" }; // IcTyre for secondary
                retreadCats = new[] { "RETD", "CASING" };
                ecomileCats = new[] { "ECOMILE" };
                break;
            default:
                locTypes = new[] { 0, 1, 3 };
                break;
        }

        param["product01"] = product01;
        param["product02"] = product02;
        param["glAccounts1"] = glAccounts;
        param["glAccounts2"] = glAccounts2;
        param["locTypes"] = locTypes;

        var where = new List<string> { "1=1" };
        if (!string.IsNullOrEmpty(custGenBusGroupCrit))
            where.Add(custGenBusGroupCrit);

        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add("Cust.[Responsibility Center] IN @respCenters");
        }
        if (p.Dealers?.Count > 0)
        {
            param["dealers"] = p.Dealers;
            where.Add("Cust.[Dealer Code] IN @dealers");
            param["filterText"] = "Dealers: " + string.Join(",", p.Dealers);
        }
        if (p.Areas?.Count > 0)
        {
            param["areas"] = p.Areas;
            where.Add("Cust.[Area Code] IN @areas");
            if (!param.ContainsKey("filterText")) param["filterText"] = "Areas: " + string.Join(",", p.Areas);
        }
        else if (p.Regions?.Count > 0)
        {
            param["regions"] = p.Regions;
            where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamsT} T ON A.[Team]=T.[Team Code] WHERE T.[Type]=6 AND T.[Code] IN @regions)");
            if (!param.ContainsKey("filterText")) param["filterText"] = "Regions: " + string.Join(",", p.Regions);
        }

        string filterTextObj = param.TryGetValue("filterText", out var ft) ? $"Filter : {ft}" : "";
        param["filterText"] = filterTextObj;

        // Ensure we format retread/ecomile parameter arrays for IN clauses securely
        string rCatsSql = string.Join(",", retreadCats.Select(c => $"'{c}'"));
        string eCatsSql = string.Join(",", ecomileCats.Select(c => $"'{c}'"));

        string entityType = (p.EntityType ?? "").Trim();
        if (!string.IsNullOrEmpty(p.EntityCode) && !string.IsNullOrEmpty(entityType))
        {
            param["userCode"] = p.EntityCode;
            if (string.Equals(entityType, "Customer", StringComparison.OrdinalIgnoreCase))
                where.Add("Cust.[No_] = @userCode");
            else if (string.Equals(entityType, "Partner", StringComparison.OrdinalIgnoreCase))
                where.Add("Cust.[Dealer Code] = @userCode");
            else if (string.Equals(entityType, "PartnerGroup", StringComparison.OrdinalIgnoreCase))
                where.Add($"Cust.[Dealer Code] IN (SELECT A.[Code] FROM {dealerT} A WHERE A.[Group] = @userCode)");
            else if (string.Equals(entityType, "Employee", StringComparison.OrdinalIgnoreCase) && string.Equals(p.EntityDepartment, "Sales", StringComparison.OrdinalIgnoreCase))
                where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamsT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode)");
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
        SELECT 
            Cust.[Name] AS CustomerName,
            Dealer.[Code] AS DealerNo,
            Dealer.[Dealership Name] AS DealerName,
            Area.[Code] + '  -  ' + Area.[Name] AS AreaName,
            Teams.[Code] AS RegionCode,
            Teams.[Name] AS RegionName,
            REPLACE(REPLACE(LEFT(Cust.[No_],3), 'CU','00'), 'C', '0')+RIGHT(Cust.[No_],5) AS CustomerNo,
            -ISNULL(GLE.Amount, 0) AS NetSale,
            -ISNULL(GLE2.Amount, 0) AS NetSale2,
            -(ISNULL(GLE.Amount, 0) + ISNULL(GLE2.Amount, 0)) AS TotalSale,
            ISNULL(Agg.RG, 0) AS RG,
            ISNULL(Agg.RR, 0) AS RR,
            ISNULL(Agg.RL, 0) AS RL,
            ISNULL(Agg.RP, 0) AS RP,
            ISNULL(Agg.RT, 0) AS RT,
            ISNULL(Agg.RO, 0) AS RO,
            ISNULL(Agg.RO1, 0) AS RO1,
            ISNULL(Agg.RG,0)+ISNULL(Agg.RR,0)+ISNULL(Agg.RL,0)+ISNULL(Agg.RP,0)+ISNULL(Agg.RT,0)+ISNULL(Agg.RO,0)+ISNULL(Agg.RO1,0) AS RTotal,
            ISNULL(Agg.EG, 0) AS EG,
            ISNULL(Agg.ER, 0) AS ER,
            ISNULL(Agg.EL, 0) AS EL,
            ISNULL(Agg.EP, 0) AS EP,
            ISNULL(Agg.ET, 0) AS ET,
            ISNULL(Agg.EO, 0) AS EO,
            ISNULL(Agg.EO1, 0) AS EO1,
            ISNULL(Agg.EG,0)+ISNULL(Agg.ER,0)+ISNULL(Agg.EL,0)+ISNULL(Agg.EP,0)+ISNULL(Agg.ET,0)+ISNULL(Agg.EO,0)+ISNULL(Agg.EO1,0) AS ETotal,
            @reportName AS ReportName,
            @period AS Period,
            @product01 AS Product01,
            @product02 AS Product02,
            CAST(@hideCustomer AS BIT) AS HideCustomer,
            CAST(@hideRegion AS BIT) AS HideRegion,
            CAST(@hideDealer AS BIT) AS HideDealer,
            CAST(@hideArea AS BIT) AS HideArea,
            @filterText AS Filter
        FROM {custT} Cust
        LEFT JOIN {dealerT} Dealer ON Dealer.[Code] = Cust.[Dealer Code]
        LEFT JOIN {areaT} Area ON Area.[Code] = Cust.[Area Code]
        LEFT JOIN {teamsT} Teams ON Teams.[Team Code] = Area.[Team] AND Teams.[Type] = 6
        LEFT JOIN (
            SELECT [Source No_] AS SourceNo, SUM([Amount]) AS Amount
            FROM {glEntryT}
            WHERE [Posting Date] >= @fromDt AND [Posting Date] <= @toDt { (glAccounts.Length > 0 ? "AND [G_L Account No_] IN @glAccounts1" : "") }
            { (type == "intercompany" ? "AND [Gen_ Prod_ Posting Group] IN ('RETD', 'CASING-PUR')" : "") }
            GROUP BY [Source No_]
        ) GLE ON GLE.SourceNo = Cust.[No_]
        LEFT JOIN (
            SELECT [Source No_] AS SourceNo, SUM([Amount]) AS Amount
            FROM {glEntryT}
            WHERE [Posting Date] >= @fromDt AND [Posting Date] <= @toDt {(glAccounts2.Length > 0 ? "AND [G_L Account No_] IN @glAccounts2" : "")}
            { (type == "intercompany" ? "AND [Gen_ Prod_ Posting Group] IN ('ECOMILE')" : "") }
            GROUP BY [Source No_]
        ) GLE2 ON GLE2.SourceNo = Cust.[No_]
        LEFT JOIN (
            SELECT 
                ILE.[Source No_] AS CustomerNo,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({rCatsSql}) AND ILE.[Product Group Code] = 'GIANT' THEN -ILE.[Quantity] ELSE 0 END) AS RG,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({rCatsSql}) AND ILE.[Product Group Code] = 'RADIAL G' THEN -ILE.[Quantity] ELSE 0 END) AS RR,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({rCatsSql}) AND ILE.[Product Group Code] = 'LCV' THEN -ILE.[Quantity] ELSE 0 END) AS RL,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({rCatsSql}) AND ILE.[Product Group Code] = 'PASS' THEN -ILE.[Quantity] ELSE 0 END) AS RP,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({rCatsSql}) AND ILE.[Product Group Code] = 'TRACTOR' THEN -ILE.[Quantity] ELSE 0 END) AS RT,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({rCatsSql}) AND ILE.[Product Group Code] = 'OTR' THEN -ILE.[Quantity] ELSE 0 END) AS RO,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({rCatsSql}) AND ILE.[Product Group Code] = 'OTR 1' THEN -ILE.[Quantity] ELSE 0 END) AS RO1,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({eCatsSql}) AND ILE.[Product Group Code] = 'GIANT' THEN -ILE.[Quantity] ELSE 0 END) AS EG,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({eCatsSql}) AND ILE.[Product Group Code] = 'RADIAL G' THEN -ILE.[Quantity] ELSE 0 END) AS ER,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({eCatsSql}) AND ILE.[Product Group Code] = 'LCV' THEN -ILE.[Quantity] ELSE 0 END) AS EL,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({eCatsSql}) AND ILE.[Product Group Code] = 'PASS' THEN -ILE.[Quantity] ELSE 0 END) AS EP,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({eCatsSql}) AND ILE.[Product Group Code] = 'TRACTOR' THEN -ILE.[Quantity] ELSE 0 END) AS ET,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({eCatsSql}) AND ILE.[Product Group Code] = 'OTR' THEN -ILE.[Quantity] ELSE 0 END) AS EO,
                SUM(CASE WHEN ILE.[Item Category Code] IN ({eCatsSql}) AND ILE.[Product Group Code] = 'OTR 1' THEN -ILE.[Quantity] ELSE 0 END) AS EO1
            FROM {itemLedgerT} ILE
            INNER JOIN {locT} Loc ON Loc.[Code] = ILE.[Location Code] { (p.RespCenters?.Count > 0 ? "AND Loc.[Responsibility Center] IN @respCenters" : "") }
            WHERE ILE.[Posting Date] >= @fromDt AND ILE.[Posting Date] <= @toDt AND ILE.[Entry Type] = 1 AND Loc.[Type] IN @locTypes
            GROUP BY ILE.[Source No_]
        ) Agg ON Agg.CustomerNo = Cust.[No_]
        {whereClause}
        ";

        var records = await scope.RawQueryToArrayAsync<TyreSalesRow>(sql, param, ct).ConfigureAwait(false);
        var rows = records.Where(r => r.NetSale != 0 || r.NetSale2 != 0 || r.RTotal != 0 || r.ETotal != 0).ToList();

        if (string.Equals(p.View, "Dealer", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.GroupBy(r => r.DealerNo).Select(g => new TyreSalesRow
            {
                ReportName = g.First().ReportName,
                HideCustomer = g.First().HideCustomer,
                HideDealer = g.First().HideDealer,
                HideRegion = g.First().HideRegion,
                HideArea = g.First().HideArea,
                Product01 = g.First().Product01,
                Product02 = g.First().Product02,
                Period = g.First().Period,
                DealerNo = g.Key,
                DealerName = g.First().DealerName,
                NetSale = g.Sum(c => c.NetSale),
                NetSale2 = g.Sum(c => c.NetSale2),
                TotalSale = g.Sum(c => c.TotalSale),
                RG = g.Sum(c => c.RG),
                RR = g.Sum(c => c.RR),
                RL = g.Sum(c => c.RL),
                RP = g.Sum(c => c.RP),
                RT = g.Sum(c => c.RT),
                RO = g.Sum(c => c.RO),
                RO1 = g.Sum(c => c.RO1),
                RTotal = g.Sum(c => c.RTotal),
                EG = g.Sum(c => c.EG),
                ER = g.Sum(c => c.ER),
                EL = g.Sum(c => c.EL),
                EP = g.Sum(c => c.EP),
                ET = g.Sum(c => c.ET),
                EO = g.Sum(c => c.EO),
                EO1 = g.Sum(c => c.EO1),
                ETotal = g.Sum(c => c.ETotal),
                Filter = g.First().Filter
            }).ToList();
        }

        return ("SalesTyre", rows.Count > 0 ? ToDataTable(rows) : null);
    }

    public async Task<List<CustomerRecord>> GetCustomerRecordsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string stateT = scope.GetQualifiedTableName("State", isShared: true);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var where = new List<string> { "1=1" };

        var customerNosFilter = SalesReportParams.ParseCustomerNos(p.Customers);
        if (customerNosFilter.Length > 0)
        {
            param["customers"] = customerNosFilter;
            where.Add("Cust.[No_] IN @customers");
        }

        if (p.Dealers?.Count > 0)
        {
            param["dealers"] = p.Dealers;
            where.Add("Cust.[Dealer Code] IN @dealers");
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
        SELECT 
            Cust.[No_] AS No, 
            Cust.[Name] AS Name, 
            Cust.[Name 2] AS Name2, 
            Cust.[Address] AS Address, 
            Cust.[Address 2] AS Address2, 
            Cust.[City] AS City, 
            Cust.[Post Code] AS PostalCode,
            Cust.[Phone No_] AS PhoneNo,
            State.[Code] AS State, 
            State.[Description] AS StateName
        FROM {custT} Cust
        LEFT JOIN {stateT} State ON State.[Code] = Cust.[State Code]
        {whereClause}";

        var records = await scope.RawQueryToArrayAsync<CustomerRecord>(sql, param, ct).ConfigureAwait(false);
        return records.ToList();
    }

    public async Task<List<RespCenter>> GetResponsibilityCentersAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        string respT = scope.GetQualifiedTableName("Responsibility Center", isShared: false);
        string stateT = scope.GetQualifiedTableName("State", isShared: true);
        string invoiceT = p.View == "CrNote" ? scope.GetQualifiedTableName("Sales Cr_Memo Header", isShared: false) : scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var conditions = new List<string>();

        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            conditions.Add("RespCenter.[Code] IN @respCenters");
        }
        else if (p.Nos?.Count > 0)
        {
            param["nos"] = p.Nos;
            conditions.Add($"RespCenter.[Code] IN (SELECT [Responsibility Center] FROM {invoiceT} WHERE [No_] IN @nos)");
        }

        string conditionClause = conditions.Count > 0 ? string.Join(" OR ", conditions) : "1=0";
        conditionClause = $"({conditionClause}) OR RespCenter.[Code] = 'BEL'";

        string sql = $@"
        SELECT 
            RespCenter.[Code] AS Code, 
            RespCenter.[Name] AS Name, 
            RespCenter.[Address] AS Address, 
            RespCenter.[Address 2] AS Address2,
            RespCenter.[City] AS City, 
            RespCenter.[Post Code] AS PostCode, 
            States.[Description] AS StateName, 
            RespCenter.[State] AS State,
            RespCenter.[Country_Region Code] AS Country, 
            RespCenter.[Contact] AS Phone, 
            RespCenter.[Company Name] AS Company,
            RespCenter.[E-Mail] AS Email, 
            RespCenter.[GST No] AS GSTRegNo, 
            RespCenter.[PAN No_] AS PANNo, 
            RespCenter.[Home Page] AS HomePage,
            RespCenter.[Juridiction] AS Juridiction, 
            RespCenter.[CIN No_] AS CINNo, 
            RespCenter.[Nature of Business] AS NatureOfBusiness, 
            RespCenter.[Logo] AS Logo
        FROM {respT} RespCenter
        LEFT JOIN {stateT} States ON States.[Code] = RespCenter.[State]
        WHERE {conditionClause}";

        var records = await scope.RawQueryToArrayAsync<RespCenter>(sql, param, ct).ConfigureAwait(false);
        return records.ToList();
    }

    private async Task<(string rdlcName, object? data)> GetSalesBelowBasePriceAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        string invLineT = scope.GetQualifiedTableName("Sales Invoice Line", isShared: false);
        string invHeaderT = scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);
        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string dealerT = scope.GetQualifiedTableName("Salesperson_Purchaser", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string locT = scope.GetQualifiedTableName("Location", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["period"] = $"For period : {fromDt:dd-MMM-yy} .. {toDt:dd-MMM-yy}",
            ["reportName"] = "Below Base Price Sales"
        };

        var where = new List<string> { "1=1" };

        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add("Cust.[Responsibility Center] IN @respCenters");
        }
        if (p.Dealers?.Count > 0)
        {
            param["dealers"] = p.Dealers;
            where.Add("Cust.[Dealer Code] IN @dealers");
        }
        if (p.Areas?.Count > 0)
        {
            param["areas"] = p.Areas;
            where.Add("Cust.[Area Code] IN @areas");
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
        SELECT 
            Cust.[No_] AS CustomerNo,
            Cust.[Name] AS CustomerName,
            Dealer.[Code] AS DealerNo,
            Dealer.[Dealership Name] AS DealerName,
            Area.[Name] AS AreaName,
            Line.[Document No_] AS InvoiceNo,
            Line.[No_] AS Tyre,
            Line.[Unit Price] AS UnitPrice,
            Line.[Base Unit Price] AS BaseUnitPrice,
            Line.[Item Category Code] AS Category,
            Line.[Base Unit Price] - Line.[Unit Price] AS Difference,
            FORMAT(Header.[Posting Date],'dd-MMM-yy') AS Date,
            @reportName AS ReportName,
            @period AS Period
        FROM {invLineT} Line
        INNER JOIN {invHeaderT} Header ON Header.[No_] = Line.[Document No_]
        LEFT JOIN {custT} Cust ON Cust.[No_] = Line.[Sell-to Customer No_]
        LEFT JOIN {dealerT} Dealer ON Dealer.[Code] = Cust.[Dealer Code]
        LEFT JOIN {areaT} Area ON Area.[Code] = Cust.[Area Code]
        LEFT JOIN {locT} Locations ON Locations.[Code] = Line.[Location Code]
        WHERE Locations.[Type] = 0
          AND Header.[Is Cancelled] = 0
          AND Line.[Unit Price] < Line.[Base Unit Price]
          AND Header.[Posting Date] >= @fromDt AND Header.[Posting Date] <= @toDt
          AND Line.[Item Category Code] IN ('RETD', 'ECOMILE')
          { (where.Count > 1 ? " AND " + string.Join(" AND ", where.Skip(1)) : "") }
        ";

        var rows = await scope.RawQueryToArrayAsync<BelowBasePriceSalesRow>(sql, param, ct).ConfigureAwait(false);
        return ("SalesBelowBasePrice", rows.Length > 0 ? ToDataTable(rows) : null);
    }

    private async Task<(string rdlcName, object? data)> GetEcomileTyreStockAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        string ecomileT = scope.GetQualifiedTableName("Ecomile Items_", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["period"] = $"For period : {fromDt:dd-MMM-yy} .. {toDt:dd-MMM-yy}"
        };

        string type = (p.Type ?? "").Trim().ToLowerInvariant();
        string reportName = "Ecomile Tyre Records";
        int? status = null;
        int? onExchange = null;

        if (type == "casing")
        {
            status = 0;
            reportName = "Ecomile Tyre Records [Casing]";
        }
        else if (type == "prod not invoice")
        {
            status = 2;
            reportName = "Ecomile Tyre Records [Prod. Not Inv]";
        }
        else if (type == "exchange scrap")
        {
            status = 1;
            onExchange = 1;
            reportName = "Ecomile Tyre Records [Exchange Scrap]";
        }
        else if (type == "rejected casing")
        {
            status = 1;
            onExchange = 0;
            reportName = "Ecomile Tyre Records [Rejected Casing]";
        }

        param["reportName"] = reportName;
        
        var where = new List<string> { "1=1" };

        if (status.HasValue)
        {
            param["status"] = status.Value;
            where.Add("[Status] = @status");
        }
        if (onExchange.HasValue)
        {
            param["onExchange"] = onExchange.Value;
            where.Add("[On Exchange] = @onExchange");
        }
        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add("[Responsibility Center] IN @respCenters");
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
        SELECT 
            [No_] AS Tyre,
            [Make] AS Make,
            FORMAT([Pur_ Date], 'dd-MMM-yy') AS PurchDate,
            FORMAT([Prod_ Date], 'dd-MMM-yy') AS ProdDate,
            ISNULL([Pattern], '') + ' ' + ISNULL([Patches Applied], '') AS ProdCondition,
            IIF(ISNULL([New Serial No_], '') <> '', [New Serial No_], [Serial No_]) AS SerialNo,
            IIF([Pur_ Date] > '1753-01-01', DATEDIFF(day, [Pur_ Date], GETDATE()), 0) AS PurchAge,
            IIF([Prod_ Date] > '1753-01-01', DATEDIFF(day, [Prod_ Date], GETDATE()), 0) AS ProdAge,
            1 AS Qty,
            (CASE [Casing Condition] 
                WHEN 0 THEN '' 
                WHEN 1 THEN 'OK' 
                WHEN 2 THEN 'Superficial Lug Damages' 
                WHEN 3 THEN 'Minor Ply Damages' 
                WHEN 4 THEN 'Minor one cut upto BP5' 
                WHEN 5 THEN 'Minor Two cuts upto BP5' 
                WHEN 6 THEN 'Minor three cuts upto BP5' 
            END) AS CasingCondition,
            @reportName AS ReportName,
            @period AS Period
        FROM {ecomileT}
        {whereClause}
        ";

        var rows = await scope.RawQueryToArrayAsync<EcomileTyreStockRow>(sql, param, ct).ConfigureAwait(false);
        return ("EcomileTyreStock", rows.Length > 0 ? ToDataTable(rows) : null);
    }

    private async Task<(string rdlcName, object? data, Dictionary<string, object?>? reportParameters)> GetCustomerTrialBalanceAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;
        DateTime fyStart = GetFiscalYearStartDate(toDt);
        const string decimalFormat = "#,##0.00";

        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string dealerT = scope.GetQualifiedTableName("Salesperson_Purchaser", isShared: false);
        string ledgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);
        string teamT = scope.GetQualifiedTableName("Team Salesperson", isShared: false);

        string fromStr = fromDt.ToString("dd-MMM-yy");
        string toStr = toDt.ToString("dd-MMM-yy");
        string fyStartStr = fyStart.ToString("dd-MMM-yy");

        var (sql, parameters) = BuildCustomerTrialBalanceSql(custT, areaT, dealerT, ledgerT, teamT, fromDt, toDt, fyStart, fromStr, toStr, fyStartStr, decimalFormat, p);

        var records = await scope.RawQueryToArrayAsync<CustomerTrialBalanceRow>(sql, parameters, ct).ConfigureAwait(false);

        if (records == null || records.Length == 0)
            return ("CustomerTrailBalance", null, new Dictionary<string, object?>());

        List<CustomerTrialBalanceRow> list = records.ToList();
        if (string.Equals(p.View, "Only Active", StringComparison.OrdinalIgnoreCase))
            list = list.Where(c => c.Active).ToList();
        else if (string.Equals(p.View, "Only Has Balance", StringComparison.OrdinalIgnoreCase))
            list = list.Where(c => c.HasBalance).ToList();

        return ("CustomerTrailBalance", list.Count > 0 ? ToDataTable(list) : null, new Dictionary<string, object?>());
    }

    private static (string sql, object parameters) BuildCustomerTrialBalanceSql(
        string custT, string areaT, string dealerT, string ledgerT, string teamT,
        DateTime fromDt, DateTime toDt, DateTime fyStart, string fromStr, string toStr, string fyStartStr, string decimalFormat,
        SalesReportParams p)
    {
        string periodFilter = $"For period : {fromStr} .. {toStr}";
        string periodFilter1 = $"{fromStr}..{toStr}";
        string fiscalYearFilter = $"{fyStartStr}..{toStr}";
        string userCode = p.EntityCode ?? "";
        string companyName = "Tyresoles (India) Pvt. Ltd.";

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["fyStart"] = fyStart,
            ["periodFilter"] = periodFilter,
            ["periodFilter1"] = periodFilter1,
            ["fiscalYearFilter"] = fiscalYearFilter,
            ["fyStartStr"] = fyStartStr,
            ["fromStr"] = fromStr,
            ["toStr"] = toStr,
            ["decimalFormat"] = decimalFormat,
            ["userCode"] = userCode,
            ["companyName"] = companyName,
            ["userName"] = $"Printed By.: {userCode}"
        };

        string typeLower = (p.Type ?? "all").Trim().ToLowerInvariant();
        int? dealerProduct = typeLower switch
        {
            "none" => 0,
            "ecomile" => 1,
            "retread" => 2,
            _ => (int?)null
        };
        param["dealerProduct"] = dealerProduct ?? -1;
        param["typeAll"] = typeLower == "all" ? 1 : 0;

        var trialBalanceCustomerNos = SalesReportParams.ParseCustomerNos(p.Customers);
        param["respCenters"] = p.RespCenters ?? Array.Empty<string>();
        param["customers"] = trialBalanceCustomerNos;
        param["dealers"] = p.Dealers ?? Array.Empty<string>();
        param["areas"] = p.Areas ?? Array.Empty<string>();
        param["regions"] = p.Regions ?? Array.Empty<string>();

        var where = new List<string> { "1=1" };
        if (dealerProduct.HasValue)
            where.Add($"(Dealer.[Product] = @dealerProduct)");
        if (p.RespCenters?.Count > 0)
            where.Add("Cust.[Responsibility Center] IN @respCenters");
        if (trialBalanceCustomerNos.Length > 0)
            where.Add("Cust.[No_] IN @customers");
        if (p.Dealers?.Count > 0)
            where.Add("Cust.[Dealer Code] IN @dealers");
        if (p.Areas?.Count > 0)
            where.Add("Cust.[Area Code] IN @areas");
        else if (p.Regions?.Count > 0)
            where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A WHERE A.[Team] IN (SELECT [Team Code] FROM {teamT} WHERE [Type]=6 AND [Code] IN @regions))");

        // EntityType from login is RespCenterUserSetup.Type: Customer, Partner, PartnerGroup, Employee. Apply scope filter.
        // PartnerGroup: entityCode is a group code; customers are linked by Dealer Code. Resolve group to dealer codes via Salesperson_Purchaser.[Group].
        string entityType = (p.EntityType ?? "").Trim();
        if (!string.IsNullOrEmpty(p.EntityCode) && !string.IsNullOrEmpty(entityType))
        {
            if (string.Equals(entityType, "Customer", StringComparison.OrdinalIgnoreCase))
                where.Add("Cust.[No_] = @userCode");
            else if (string.Equals(entityType, "Partner", StringComparison.OrdinalIgnoreCase))
                where.Add("Cust.[Dealer Code] = @userCode");
            else if (string.Equals(entityType, "PartnerGroup", StringComparison.OrdinalIgnoreCase))
                where.Add($"Cust.[Dealer Code] IN (SELECT [Code] FROM {dealerT} WHERE [Group] = @userCode)");
            else if (string.Equals(entityType, "Employee", StringComparison.OrdinalIgnoreCase) && string.Equals(p.EntityDepartment, "Sales", StringComparison.OrdinalIgnoreCase))
                where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A WHERE A.[Team] IN (SELECT [Team Code] FROM {teamT} WHERE [Code] = @userCode))");
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
SELECT
    Cust.[Name] AS Name_Customer,
    Area.[Name] AS AreaName,
    Cust.[Area Code] AS AreaCode_Customer,
    Cust.[Dealer Code] AS DealerCode_Customer,
    Dealer.[Dealership Name] AS DealerName,
    REPLACE(REPLACE(LEFT(Cust.[No_],3), 'CU','00'), 'C', '0') + RIGHT(Cust.[No_],5) AS No_Customer,
    ISNULL(PeriodOBal.Amount, 0) AS PeriodBeginBalance,
    ISNULL(FYOBal.Amount, 0) AS YTDBeginBalance,
    ISNULL(Ledger.Credit, 0) AS PeriodCreditAmt,
    ISNULL(Ledger.Debit, 0) AS PeriodDebitAmt,
    ISNULL(LedgerFY.Credit, 0) AS YTDCreditAmt,
    ISNULL(LedgerFY.Debit, 0) AS YTDDebitAmt,
    ISNULL(PeriodOBal.Amount, 0) - ISNULL(Ledger.Credit, 0) + ISNULL(Ledger.Debit, 0) AS YTDTotal,
    IIF((ISNULL(PeriodOBal.Amount, 0) - ISNULL(Ledger.Credit, 0) + ISNULL(Ledger.Debit, 0) - ISNULL(LedgerFY.Credit, 0) + ISNULL(LedgerFY.Debit, 0)) = 0, 0, 1) AS HasBalance,
    IIF((ISNULL(Ledger.Credit, 0) > 0) AND (ISNULL(Ledger.Debit, 0) > 0), 1, 0) AS Active,
    'Customer - Trial Balance' AS CustTrialBalanceCaption,
    1 AS bSorting,
    @companyName AS CompanyName,
    @periodFilter AS PeriodFilter,
    @fromStr AS PeriodStartDateStr,
    @periodFilter1 AS PeriodFilter1,
    @fyStartStr AS FiscalYearStartDateStr,
    @fiscalYearFilter AS FiscalYearFilter,
    @toStr AS PeriodEndDateStr,
    @userName AS UserName,
    'Customer No' AS No_CustomerCaption,
    'Name' AS Name_CustomerCaption,
    'Beginning Balance' AS PeriodBeginBalanceCaption,
    'Credit' AS PeriodCreditAmtCaption,
    'Debit' AS PeriodDebitAmtCaption,
    CAST(@fromDt AS DATE) AS PeriodStartDate,
    CAST(@fyStart AS DATE) AS FiscalYearStartDate,
    @fiscalYearFilter AS FiscalYearFilter,
    CAST(@toDt AS DATE) AS PeriodEndDate,
    'Ending Balance' AS YTDTotalCaption,
    'Total in LCY' AS TotalinLCYCaption,
    @decimalFormat AS PeriodBeginBalanceFormat,
    @decimalFormat AS PeriodCreditAmtFormat,
    @decimalFormat AS PeriodDebitAmtFormat,
    @decimalFormat AS YTDBeginBalanceFormat,
    @decimalFormat AS YTDCreditAmtFormat,
    @decimalFormat AS YTDDebitAmtFormat,
    @decimalFormat AS YTDTotalFormat,
    'Period' AS PeriodCaption,
    'Fiscal Year To Date' AS FiscalYearToDateCaption,
    'Net Change' AS NetChangeCaption
FROM {custT} Cust
LEFT JOIN {areaT} Area ON Area.[Code] = Cust.[Area Code]
LEFT JOIN {dealerT} Dealer ON Dealer.[Code] = Cust.[Dealer Code]
LEFT JOIN (
    SELECT [Customer No_] AS CustomerNo, SUM([Amount]) AS Amount
    FROM {ledgerT}
    WHERE [Posting Date] < @fromDt AND [Entry Type] <> 2
    GROUP BY [Customer No_]
) PeriodOBal ON PeriodOBal.CustomerNo = Cust.[No_]
LEFT JOIN (
    SELECT [Customer No_] AS CustomerNo, SUM([Amount]) AS Amount
    FROM {ledgerT}
    WHERE [Posting Date] < @fyStart AND [Entry Type] <> 2
    GROUP BY [Customer No_]
) FYOBal ON FYOBal.CustomerNo = Cust.[No_]
LEFT JOIN (
    SELECT [Customer No_] AS CustomerNo, SUM([Debit Amount]) AS Debit, SUM([Credit Amount]) AS Credit
    FROM {ledgerT}
    WHERE [Posting Date] >= @fromDt AND [Posting Date] <= @toDt AND [Entry Type] <> 2
    GROUP BY [Customer No_]
) Ledger ON Ledger.CustomerNo = Cust.[No_]
LEFT JOIN (
    SELECT [Customer No_] AS CustomerNo, SUM([Debit Amount]) AS Debit, SUM([Credit Amount]) AS Credit
    FROM {ledgerT}
    WHERE [Posting Date] >= @fyStart AND [Posting Date] <= @toDt AND [Entry Type] <> 2
    GROUP BY [Customer No_]
) LedgerFY ON LedgerFY.CustomerNo = Cust.[No_]
{whereClause}";

        return (sql, param);
    }

    private static DateTime GetFiscalYearStartDate(DateTime date)
    {
        return new DateTime(date.Month < 4 ? date.Year - 1 : date.Year, 4, 1);
    }

    private static string FormatCustomerNo(string? no)
    {
        if (string.IsNullOrEmpty(no)) return "";
        var prefix = no.Length >= 3 ? no.Substring(0, 3) : no;
        var suffix = no.Length >= 5 ? no.Substring(no.Length - 5) : no;
        var formattedPrefix = prefix.Replace("CU", "00").Replace("C", "0");
        return formattedPrefix + suffix;
    }

    private sealed class SumRow
    {
        public string? CustomerNo { get; set; }
        public decimal Amount { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    private async Task<(string rdlcName, object? data, Dictionary<string, object?>? reportParameters)> GetStatementOfAccountAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string dealerT = scope.GetQualifiedTableName("Salesperson_Purchaser", isShared: false);
        string teamT = scope.GetQualifiedTableName("Team Salesperson", isShared: false);
        string ledgerT = scope.GetQualifiedTableName("Cust_ Ledger Entry", isShared: false);
        string detailedLedgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);
        string invoiceT = scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);
        string creditMemoT = scope.GetQualifiedTableName("Sales Cr_Memo Header", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["companyName"] = "Tyresoles (India) Pvt. Ltd.",
            ["periodText"] = $"Period from: {fromDt:dd-MMM-yyyy} to: {toDt:dd-MMM-yyyy}",
            ["userCode"] = p.EntityCode ?? ""
        };

        var where = new List<string> { "1=1" };
        if (p.Dealers?.Count > 0)
        {
            param["dealers"] = p.Dealers;
            where.Add("Cust.[Dealer Code] IN @dealers");
        }
        var statementCustomerNos = SalesReportParams.ParseCustomerNos(p.Customers);
        if (statementCustomerNos.Length > 0)
        {
            param["customers"] = statementCustomerNos;
            where.Add("Cust.[No_] IN @customers");
        }
        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add("Cust.[Responsibility Center] IN @respCenters");
        }
        if (p.Areas?.Count > 0)
        {
            param["areas"] = p.Areas;
            where.Add("Cust.[Area Code] IN @areas");
        }

        string entityType = (p.EntityType ?? "").Trim();
        if (!string.IsNullOrEmpty(p.EntityCode) && !string.IsNullOrEmpty(entityType))
        {
            if (string.Equals(entityType, "Customer", StringComparison.OrdinalIgnoreCase))
                where.Add("Cust.[No_] = @userCode");
            else if (string.Equals(entityType, "Partner", StringComparison.OrdinalIgnoreCase))
                where.Add("Cust.[Dealer Code] = @userCode");
            else if (string.Equals(entityType, "PartnerGroup", StringComparison.OrdinalIgnoreCase))
                where.Add($"Cust.[Dealer Code] IN (SELECT [Code] FROM {dealerT} WHERE [Group] = @userCode)");
            else if (string.Equals(entityType, "Employee", StringComparison.OrdinalIgnoreCase) && string.Equals(p.EntityDepartment, "Sales", StringComparison.OrdinalIgnoreCase))
                where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A WHERE A.[Team] IN (SELECT [Team Code] FROM {teamT} WHERE [Code] = @userCode))");
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
        SELECT 
            Ledger.[External Document No_] AS ExternalDocumentNo_CustLedgerEntry,
            Ledger.[Customer No_] AS CustomerNo_CustLedgerEntry,
            Ledger.[Posting Date] AS PostingDate_CustLedgerEntry,
            Ledger.[Document Type] AS DocumentType,
            Ledger.[Document No_] AS DocumentNo_CustLedgerEntry,
            ISNULL(OBLedger.Balance, 0) AS OpnBalance,
            ISNULL(Detail.Amount, 0) AS Amount_CustLedgerEntry,
            ISNULL(Detail.Debit, 0) AS DebitAmount_CustLedgerEntry,
            ISNULL(Detail.Credit, 0) AS CreditAmount_CustLedgerEntry,
            Ledger.[Description] AS Description,
            Ledger.[Source Code] AS SourceCode_CustLedgerEntry,
            Invoice.[Sell-to Customer Name] AS InvoiceName,
            CreditMemo.[Sell-to Customer Name] AS CreditMemoName,
            @companyName AS COMPANYNAME,
            @periodText AS PeriodText,
            Cust.[Address] AS Add1,
            Cust.[Address 2] AS Add2,
            Cust.[City] AS Add3,
            Cust.[State Code] AS Add4,
            Cust.[Post Code] AS Add5,
            Area.[Name] AS AreaName,
            Dealer.[Dealership Name] AS DealerName,
            Cust.[Responsibility Center] AS RespCenter,
            @userCode AS UserName,
            Ledger.[Entry No_] AS EntryNo
        FROM {custT} Cust
        INNER JOIN {ledgerT} Ledger ON Ledger.[Customer No_] = Cust.[No_] AND Ledger.[Posting Date] >= @fromDt AND Ledger.[Posting Date] <= @toDt
        LEFT JOIN (
            SELECT [Cust_ Ledger Entry No_] AS CustLedgerEntryNo, SUM([Amount]) AS Amount, SUM([Credit Amount]) AS Credit, SUM([Debit Amount]) AS Debit
            FROM {detailedLedgerT}
            WHERE [Entry Type] IN (0, 1) AND [Posting Date] >= @fromDt AND [Posting Date] <= @toDt
            GROUP BY [Cust_ Ledger Entry No_]
        ) Detail ON Detail.CustLedgerEntryNo = Ledger.[Entry No_]
        LEFT JOIN {invoiceT} Invoice ON Invoice.[No_] = Ledger.[Document No_]
        LEFT JOIN {creditMemoT} CreditMemo ON CreditMemo.[No_] = Ledger.[Document No_]
        LEFT JOIN (
            SELECT [Customer No_] AS CustomerNo, SUM([Amount]) AS Balance
            FROM {detailedLedgerT}
            WHERE [Posting Date] < @fromDt
            GROUP BY [Customer No_]
        ) OBLedger ON OBLedger.CustomerNo = Cust.[No_]
        LEFT JOIN {areaT} Area ON Area.[Code] = Cust.[Area Code]
        LEFT JOIN {dealerT} Dealer ON Dealer.[Code] = Cust.[Dealer Code]
        {whereClause}
        ORDER BY Cust.[No_], Ledger.[Posting Date], Ledger.[Entry No_]";

        var records = (await scope.RawQueryToArrayAsync<dynamic>(sql, param, ct).ConfigureAwait(false))
            .Where(r => r.CustomerNo_CustLedgerEntry != null)
            .ToList();

        if (records.Count == 0) return ("StatementOfAccount", null, new Dictionary<string, object?>());

        var respCenters = await GetResponsibilityCentersAsync(scope, p, ct).ConfigureAwait(false);
        var customers = await GetCustomerRecordsAsync(scope, p, ct).ConfigureAwait(false);

        var list = new List<StatementOfAccount>();
        string custNo = string.Empty;
        decimal bal = 0, opnBal = 0;

        foreach (var rec in records)
        {
            var record = new StatementOfAccount();
            
            var respCenter = respCenters.FirstOrDefault(c => c.Code == (string?)rec.RespCenter);
            if (respCenter != null && !string.IsNullOrWhiteSpace(respCenter.Code))
            {
                record.CompAdd1 = respCenter.GetAddress1();
                record.CompAdd2 = respCenter.GetAddress2();
                record.CompAdd3 = respCenter.GetContactLine();
                record.CompAdd4 = respCenter.GetContactWebLine();
            }

            Console.WriteLine(record.CompAdd1);
            var customer = customers.FirstOrDefault(c => c.No == (string?)rec.CustomerNo_CustLedgerEntry);
            if (customer != null && !string.IsNullOrWhiteSpace(customer.No))
            {
                var addresses = customer.GetAddress();
                record.Add1 = addresses.Count >= 1 ? addresses[0] : "";
                record.Add2 = addresses.Count >= 2 ? addresses[1] : "";
                record.Add3 = addresses.Count >= 3 ? addresses[2] : "";
                record.Add4 = addresses.Count >= 4 ? addresses[3] : "";
                record.Add5 = addresses.Count >= 5 ? addresses[4] : "";
            }

            record.ExternalDocumentNo_CustLedgerEntry = rec.ExternalDocumentNo_CustLedgerEntry;
            record.CustomerNo_CustLedgerEntry = rec.CustomerNo_CustLedgerEntry;
            record.DealerName = rec.DealerName;
            record.PostingDate_CustLedgerEntry = rec.PostingDate_CustLedgerEntry ?? DateTime.MinValue;
            
            int docTypeInt = rec.DocumentType != null ? (int)rec.DocumentType : 0;
            record.DocumentType_CustLedgerEntry = docTypeInt == 0 ? " " : docTypeInt.ToString();
            
            record.DocumentNo_CustLedgerEntry = rec.DocumentNo_CustLedgerEntry;
            record.OpnBalance = rec.OpnBalance ?? 0m;
            record.Amount_CustLedgerEntry = rec.Amount_CustLedgerEntry ?? 0m;
            record.CreditAmount_CustLedgerEntry = rec.CreditAmount_CustLedgerEntry ?? 0m;
            record.DebitAmount_CustLedgerEntry = rec.DebitAmount_CustLedgerEntry ?? 0m;

            if (custNo != rec.CustomerNo_CustLedgerEntry)
            {
                bal = 0;
                opnBal = rec.OpnBalance ?? 0m;
                custNo = rec.CustomerNo_CustLedgerEntry;
            }
            if (rec.Amount_CustLedgerEntry != 0)
                bal += (decimal)(rec.Amount_CustLedgerEntry ?? 0m);

            record.CustBalance = opnBal + bal;
            string CrDr = record.CustBalance > 0 ? "Dr" : "Cr";
            record.BalanceText = $"{record.CustBalance:N2} {CrDr}";
            record.SourceCode_CustLedgerEntry = rec.SourceCode_CustLedgerEntry;
            record.AreaName = rec.AreaName;
            record.COMPANYNAME = "Tyresoles (India) Pvt. Ltd.";
            record.PeriodText = $"Period from: {fromDt:dd-MMM-yyyy} .. {toDt:dd-MMM-yyyy}";
            
            record.UserName = p.EntityCode;

            switch (docTypeInt)
            {
                case 2: // Invoice
                    record.DocTypeColor = "DarkBlue";
                    record.SellToName = rec.InvoiceName;
                    record.DocumentType_CustLedgerEntry = "Invoice";
                    break;
                case 3: // CreditMemo
                    record.DocTypeColor = "Gray";
                    record.SellToName = rec.CreditMemoName;
                    record.DocumentType_CustLedgerEntry = "CreditMemo";
                    break;
                case 0: // None
                    record.DocTypeColor = "Black";
                    record.DocumentType_CustLedgerEntry = "";
                    if (!string.IsNullOrWhiteSpace((string?)rec.Description))
                        record.SellToName = rec.Description;
                    break;
                case 1: // Payment
                case 4: // Refund
                default:
                    record.DocTypeColor = "DarkGreen";
                    if (string.Equals((string?)rec.SourceCode_CustLedgerEntry, "SALES", StringComparison.OrdinalIgnoreCase))
                        record.SellToName = " Contract Charges. ";
                    else
                        record.SellToName = rec.Description;
                    record.DocumentType_CustLedgerEntry = docTypeInt == 1 ? "Payment" : "Refund";

                    break;
            }

            if (string.Equals(p.Type, "Hide Dealer Name", StringComparison.OrdinalIgnoreCase))
                record.bHideDealer = true;

            list.Add(record);
        }

        return ("StatementOfAccount", list.Count > 0 ? ToDataTable(list) : null, new Dictionary<string, object?>());
    }

    private async Task<(string rdlcName, object? data)> GetEcomileItemSoldAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        string itemLedgerT = scope.GetQualifiedTableName("Item Ledger Entry", isShared: false);
        string locT = scope.GetQualifiedTableName("Location", isShared: false);
        string itemT = scope.GetQualifiedTableName("Item", false);
        string itemVariantT = scope.GetQualifiedTableName("Item Variant", false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["period"] = $"{fromDt:dd-MMM-yy} to {toDt:dd-MMM-yy}",
            ["reportName"] = "Ecomile Item Sold"
        };

        var where = new List<string> { "1=1" };
        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add($"[Location Code] IN (SELECT [Code] FROM {locT} WHERE [Responsibility Center] IN @respCenters AND [Type]=0)");
        }

        string whereClause = where.Count > 1 ? " AND " + string.Join(" AND ", where.Skip(1)) : "";

        string sql = $@"
        SELECT 
            Item.[Alternative Item No_] AS Tyre,
            Variant.Pattern as Pattern,            
            -SUM(Ledger.[Quantity]) AS Quantity,
            'Locations : '+@respCenters as Location,
            @reportName AS ReportName,
            @period AS DateRange
        FROM {itemLedgerT} AS Ledger
        LEFT JOIN (SELECT * FROM {itemT}) as Item
        ON Item.No_ = Ledger.[Item No_]
        LEFT JOIN (SELECT * FROM {itemVariantT}) AS Variant
        ON Variant.[Item No_] = Ledger.[Item No_] AND Variant.Code = Ledger.[Variant Code]
        WHERE Ledger.[Item Category Code] = 'ECOMILE' AND Ledger.[Entry Type] = 1
        AND Ledger.[Variant Code] <> 'ECOMILE'
        AND Ledger.[Posting Date] >= @fromDt AND Ledger.[Posting Date] <= @toDt
        {whereClause}
        GROUP BY Item.[Alternative Item No_], [Variant].[Pattern]
        ORDER BY Item.[Alternative Item No_], [Variant].[Pattern]
        ";

        var rows = await scope.RawQueryToArrayAsync<EcomileItemSoldRow>(sql, param, ct).ConfigureAwait(false);
        return ("EcomileItemSold", rows.Length > 0 ? ToDataTable(rows) : null);
    }

    private async Task<(string rdlcName, object? data)> GetDayTransactionAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today;
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : fromDt;

        string ledgerT = scope.GetQualifiedTableName("Cust_ Ledger Entry", isShared: false);
        string detailedLedgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);
        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string invT = scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);
        string crnT = scope.GetQualifiedTableName("Sales Cr_Memo Header", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string teamsT = scope.GetQualifiedTableName("Team Salesperson", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["period"] = $"For Period {fromDt:dd-MMM-yyyy} - {toDt:dd-MMM-yyyy}"
        };

        var where = new List<string> { "1=1" };

        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add("Customer.[Responsibility Center] IN @respCenters");
        }
        if (p.Dealers?.Count > 0)
        {
            param["dealers"] = p.Dealers;
            where.Add("Customer.[Dealer Code] IN @dealers");
        }
        if (p.Areas?.Count > 0)
        {
            param["areas"] = p.Areas;
            where.Add("Customer.[Area Code] IN @areas");
        }
        else if (p.Regions?.Count > 0)
        {
            param["regions"] = p.Regions;
            where.Add($"Customer.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamsT} T ON A.[Team]=T.[Team Code] WHERE T.[Type]=6 AND T.[Code] IN @regions)");
        }

        string entityType = (p.EntityType ?? "").Trim();
        if (!string.IsNullOrEmpty(p.EntityCode) && !string.IsNullOrEmpty(entityType))
        {
            param["userCode"] = p.EntityCode;
            if (string.Equals(entityType, "Customer", StringComparison.OrdinalIgnoreCase))
                where.Add("Customer.[No_] = @userCode");
            else if (string.Equals(entityType, "Partner", StringComparison.OrdinalIgnoreCase))
                where.Add("Customer.[Dealer Code] = @userCode");
            else if (string.Equals(entityType, "Employee", StringComparison.OrdinalIgnoreCase) && string.Equals(p.EntityDepartment, "Sales", StringComparison.OrdinalIgnoreCase))
                where.Add($"Customer.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamsT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode)");
        }

        string whereClause = " WHERE " + string.Join(" AND ", where);

        string sql = $@"
        SELECT 
            Ledger.[Customer No_] AS CustomerNo,
            Ledger.[External Document No_] AS ExtDocNo,
            Ledger.[Posting Date] AS PostingDate,
            Ledger.[Document No_] AS DocumentNo,
            Customer.[Name] AS Name,
            SUM(DetLedger.[Amount]) AS Amount,
            (CASE 
                WHEN Ledger.[Document Type] IN (1,2) AND Ledger.[Source Code] = 'SALES' THEN Inv.[Sell-to Customer Name]
                WHEN Ledger.[Document Type] IN (3) AND Ledger.[Source Code] <> 'SALES' THEN Crn.[Sell-to Customer Name]
                ELSE Ledger.[Description] 
            END) AS Description,
            ISNULL((
                SELECT SUM(DL.[Amount])
                FROM {detailedLedgerT} DL
                INNER JOIN {ledgerT} Ldg ON DL.[Cust_ Ledger Entry No_] = Ldg.[Entry No_]
                WHERE Ldg.[Customer No_] = Ledger.[Customer No_] AND Ldg.[Posting Date] < Ledger.[Posting Date]
            ), 0) AS OpeningBal
        FROM {ledgerT} Ledger
        INNER JOIN {custT} Customer ON Customer.[No_] = Ledger.[Customer No_]
        LEFT JOIN {detailedLedgerT} DetLedger ON DetLedger.[Cust_ Ledger Entry No_] = Ledger.[Entry No_]
        LEFT JOIN {invT} Inv ON Inv.[No_] = Ledger.[Document No_]
        LEFT JOIN {crnT} Crn ON Crn.[No_] = Ledger.[Document No_]
        WHERE Ledger.[Posting Date] >= @fromDt AND Ledger.[Posting Date] <= @toDt
        { (where.Count > 1 ? " AND " + string.Join(" AND ", where.Skip(1)) : "") }
        GROUP BY 
            Ledger.[Customer No_], 
            Ledger.[Posting Date], 
            Ledger.[External Document No_], 
            Ledger.[Document No_], 
            Ledger.[Document Type], 
            Ledger.[Description], 
            Inv.[Sell-to Customer Name], 
            Crn.[Sell-to Customer Name], 
            Ledger.[Source Code], 
            Customer.[Name]
        ";

        var records = await scope.RawQueryToArrayAsync<DayTransactionRow>(sql, param, ct).ConfigureAwait(false);

        var list = records.OrderBy(c => c.CustomerNo).ThenBy(c => c.PostingDate).ToList();
        var grpCust = list.GroupBy(c => c.CustomerNo).Select(c => c.FirstOrDefault()).ToList();

        foreach (var cust in grpCust)
        {
            if (cust == null) continue;
            var grpDate = list.Where(c => c.CustomerNo == cust.CustomerNo).GroupBy(c => c.PostingDate).Select(c => c.FirstOrDefault()).ToList();
            foreach (var dt in grpDate)
            {
                if (dt == null) continue;
                var dayRecs = list.Where(c => c.CustomerNo == cust.CustomerNo && c.PostingDate == dt.PostingDate).ToList();
                if (dayRecs.Count == 0) continue;

                decimal bal = dayRecs.First().OpeningBal;
                foreach (var rec in dayRecs)
                {
                    bal += rec.Amount;
                    rec.Balance = bal;
                    rec.UserName = p.EntityCode;
                    rec.PeriodText = param["period"]?.ToString();
                }
            }
        }

        return ("DayTransactionReport", list.Count > 0 ? ToDataTable(list) : null);
    }

    private async Task<(string rdlcName, object? data)> GetProductMixEcomileAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        var locations = await GetLocationRespCentersAsync(scope, new[] { 0 }, new[] { 0 }, ct).ConfigureAwait(false);
        
        var list = new List<ProductMixEcomileRow>();
        int ordNo = 1;

        foreach (var location in locations.OrderBy(l => l.RespCenterName))
        {
            var locItems = await GetProductMixEcomileDataAsync(scope, p, new[] { location.LocationCode! }, fromDt, toDt, ct).ConfigureAwait(false);
            foreach (var item in locItems)
            {
                item.Location = $"{ordNo:D2} {location.RespCenterName}";
                item.PeriodText = $"Period from: {fromDt:dd-MMM-yyyy} to: {toDt:dd-MMM-yyyy}";
                item.ReportName = "Product Mix (Ecomile)";
                item.CompanyName = "Tyresoles (India) Pvt. Ltd.";
            }
            if (locItems.Count > 0)
            {
                list.AddRange(locItems);
                ordNo++;
            }
        }

        // Consolidated
        if (locations.Count > 0)
        {
            var allLocCodes = locations.Select(l => l.LocationCode!).ToArray();
            var consolidatedItems = await GetProductMixEcomileDataAsync(scope, p, allLocCodes, fromDt, toDt, ct).ConfigureAwait(false);
            foreach (var item in consolidatedItems)
            {
                item.Location = "00 CONSOLIDATED";
                item.PeriodText = $"Period from: {fromDt:dd-MMM-yyyy} to: {toDt:dd-MMM-yyyy}";
                item.ReportName = "Product Mix (Ecomile)";
                item.CompanyName = "Tyresoles (India) Pvt. Ltd.";
            }
            list.AddRange(consolidatedItems);
        }

        return ("ProductMixEcomile", list.Count > 0 ? ToDataTable(list) : null);
    }

    private async Task<List<ProductMixEcomileRow>> GetProductMixEcomileDataAsync(ITenantScope scope, SalesReportParams p, string[] locCodes, DateTime fromDt, DateTime toDt, CancellationToken ct)
    {
        string itemT = scope.GetQualifiedTableName("Item", isShared: false);
        string ileT = scope.GetQualifiedTableName("Item Ledger Entry", isShared: false);
        string veT = scope.GetQualifiedTableName("Value Entry", isShared: false);
        string custT = scope.GetQualifiedTableName("Customer", isShared: false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["locCodes"] = locCodes
        };

        string sql = $@"
        SELECT 
            Items.No_ as ItemNo, 
            Items.[Alternative Item No_] as ItemGroup,
            ISNULL(Ledger.Qty, 0) as Qty,
            ISNULL(ValueLedger.Amount, 0) as SalesAmount,
            CASE WHEN ISNULL(Ledger.Qty, 0) <> 0 THEN ISNULL(ValueLedger.Amount, 0) / Ledger.Qty ELSE 0 END AS UnitPrice
        FROM {itemT} as Items
        LEFT JOIN (
            SELECT ILE.[Item No_], -SUM(ILE.[Quantity]) as Qty
            FROM {ileT} AS ILE
            JOIN {custT} AS Cust ON Cust.[No_] = ILE.[Source No_]
            WHERE ILE.[Entry Type] = 1 
              AND ILE.[Posting Date] >= @fromDt AND ILE.[Posting Date] <= @toDt
              AND ILE.[Location Code] IN @locCodes
              AND Cust.[Gen_ Bus_ Posting Group] IN ('SALES', 'EXEMPT', 'EXPORT')
            GROUP BY ILE.[Item No_]
        ) AS Ledger ON Ledger.[Item No_] = Items.No_
        LEFT JOIN (
            SELECT VE.[Item No_], -SUM(VE.[Sales Amount (Actual)]) as Amount
            FROM {veT} AS VE
            JOIN {custT} AS Cust ON Cust.[No_] = VE.[Source No_]
            WHERE VE.[Posting Date] >= @fromDt AND VE.[Posting Date] <= @toDt
              AND VE.[Location Code] IN @locCodes
              AND Cust.[Gen_ Bus_ Posting Group] IN ('SALES', 'EXEMPT', 'EXPORT')
            GROUP BY VE.[Item No_]
        ) AS ValueLedger ON ValueLedger.[Item No_] = Items.No_
        WHERE Items.[Item Category Code] = 'ECOMILE' 
          AND (ISNULL(Ledger.Qty, 0) <> 0 OR ISNULL(ValueLedger.Amount, 0) <> 0)
        ORDER BY Items.[Alternative Item No_]";

        var result = await scope.RawQueryToArrayAsync<ProductMixEcomileRow>(sql, param, ct).ConfigureAwait(false);
        return result.ToList();
    }

    private async Task<List<LocationRespCenter>> GetLocationRespCentersAsync(ITenantScope scope, int[] locTypes, int[] respTypes, CancellationToken ct)
    {
        string locT = scope.GetQualifiedTableName("Location", isShared: false);
        string respT = scope.GetQualifiedTableName("Responsibility Center", isShared: false);

        var param = new Dictionary<string, object?>();
        var where = new List<string> { "1=1" };

        if (locTypes != null && locTypes.Length > 0)
        {
            param["locTypes"] = locTypes;
            where.Add("Locations.[Type] IN @locTypes");
        }

        if (respTypes != null && respTypes.Length > 0)
        {
            param["respTypes"] = respTypes;
            where.Add("RespCenter.[Nature of Business] IN @respTypes");
        }

        string sql = $@"
        SELECT 
            Locations.[Code] AS LocationCode, 
            RespCenter.[Code] AS RespCenterCode, 
            RespCenter.[Name] AS RespCenterName
        FROM {locT} AS Locations
        LEFT JOIN {respT} AS RespCenter ON RespCenter.[Code] = Locations.[Responsibility Center]
        WHERE " + string.Join(" AND ", where);

        var result = await scope.RawQueryToArrayAsync<LocationRespCenter>(sql, param, ct).ConfigureAwait(false);
        return result.ToList();
    }

    private async Task<(string rdlcName, object? data)> GetEcomileTyreMakeAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.Today.AddMonths(-1);
        DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.Today;

        string itemLedgerT = scope.GetQualifiedTableName("Item Ledger Entry", isShared: false);
        string locT = scope.GetQualifiedTableName("Location", isShared: false);
        string custT = scope.GetQualifiedTableName("Customer", false);

        var param = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDt"] = fromDt,
            ["toDt"] = toDt,
            ["period"] = $"{fromDt:dd-MMM-yy} to {toDt:dd-MMM-yy}",
            ["reportName"] = "Size / Make (Ecomile)"
        };

        var where = new List<string> { "1=1" };
        if (p.RespCenters?.Count > 0)
        {
            param["respCenters"] = p.RespCenters;
            where.Add($"Ledger.[Location Code] IN (SELECT [Code] FROM {locT} WHERE [Responsibility Center] IN @respCenters)");
        }

        string whereClause = where.Count > 1 ? " AND " + string.Join(" AND ", where.Skip(1)) : "";

        string sql = $@"
        SELECT 
            [Item No_] AS Tyre,
            [Make] AS Make,
            -SUM([Quantity]) AS Quantity,
            @reportName AS ReportName,
            'For Period '+@period AS DateRange
        FROM {itemLedgerT} as Ledger
        LEFT JOIN (SELECT * FROM {custT}) as Customers
        ON Customers.No_ = Ledger.[Source No_]
        WHERE Ledger.[Item Category Code] = 'ECOMILE' AND Ledger.[Entry Type] = 1
        AND Customers.[Gen_ Bus_ Posting Group] = 'SALES'
        AND Ledger.[Posting Date] >= @fromDt AND Ledger.[Posting Date] <= @toDt
        {whereClause}
        GROUP BY Ledger.[Item No_], Ledger.[Make]
        ORDER BY Ledger.[Item No_], Ledger.[Make]
        ";

        var rows = await scope.RawQueryToArrayAsync<EcomileSizeMakeRow>(sql, param, ct).ConfigureAwait(false);
        return ("EcomileSizeMake", rows.Length > 0 ? ToDataTable(rows) : null);
    }

    public static List<string> GetGLAccountForSale(SaleType type)
    {
        switch (type)
        {
            case SaleType.Scrap:
                return new List<string> { "3127", "3612", "3615", "3624", "1515", "3130" };
            case SaleType.FlapTube:
                return new List<string> { "3129" };
            case SaleType.Ecomile:
                return new List<string> { "3126", "7573" };
            case SaleType.Retread:
                return new List<string> { "3110", "7572" };
            case SaleType.ExchangeTyre:
                return new List<string> { "4138" };
            case SaleType.IcTyre:
                return new List<string> { "3302", "3303" };
            case SaleType.Ecoflex:
                return new List<string> { "3112", "3113", "3115", "3117", "3118", "3304", "3317", "3625" };
            case SaleType.IcEcoflex:
                return new List<string> { "3302", "3303", "3304" };
            case SaleType.TreadRubber:
                return new List<string> { "1513", "3303", "3305" };
            default:
                return new List<string>();                    
        }
    }

    public static List<string> GetGenBusPostingGroupForSale(SaleType type)
    {
        switch (type)
        {
            case SaleType.Scrap:
                return new List<string> { "SALES"};
            case SaleType.FlapTube:
                return new List<string> { "SALES" };
            case SaleType.Ecomile:
                return new List<string> { "SALES" };
            case SaleType.Retread:
                return new List<string> { "SALES" };
            case SaleType.IcTyre:
                return new List<string> { "IC-OS", "IC-WS" };
            case SaleType.Ecoflex:
                return new List<string> { "SALES" };
            case SaleType.IcEcoflex:
                return new List<string> { "IC-OS", "IC-WS" };
            case SaleType.TreadRubber:
                return new List<string> { "SALES" };
            default:
                return new List<string>();
        }
    }

    public static List<ItemCategoryProductGroup> Products()
    {
        List<ItemCategoryProductGroup> products = new List<ItemCategoryProductGroup>();
        #region Retread
        products.Add(new ItemCategoryProductGroup
        {
            Id = 1,
            SaleType = SaleType.Retread,
            Product = "Retread Tyre",                
            ItemCategories = new List<string> { "RETD" },
            ProductGroup = "GIANT",
            Unit = 1
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 2,
            SaleType = SaleType.Retread,
            Product = "Retread Tyre",                
            ItemCategories = new List<string> { "RETD" },
            ProductGroup = "RADIAL G",
            Unit = 1
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=3,
            SaleType = SaleType.Retread,
            Product = "Retread Tyre",                
            ItemCategories = new List<string> { "RETD" },
            ProductGroup = "LCV",
            Unit = 0.5
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id =4,
            SaleType = SaleType.Retread,
            Product = "Retread Tyre",                
            ItemCategories = new List<string> { "RETD" },
            ProductGroup = "PASS",
            Unit = 0.33
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=5,
            SaleType = SaleType.Retread,
            Product = "Retread Tyre",                
            ItemCategories = new List<string> { "RETD" },
            ProductGroup = "TRACTOR",
            Unit = 2
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=6,
            SaleType = SaleType.Retread,
            Product = "Retread Tyre",
            ItemCategories = new List<string> { "RETD" },
            ProductGroup = "OTR",
            Unit = 7.00
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=7,
            SaleType = SaleType.Retread,
            Product = "Retread Tyre",
            ItemCategories = new List<string> { "RETD" },
            ProductGroup = "OTR 1",
            Unit = 3.00
        });
        #endregion Retread
        #region Ecomile
        products.Add(new ItemCategoryProductGroup
        {
            Id=1,
            SaleType = SaleType.Ecomile,
            Product = "Ecomile Tyre",
            ItemCategories = new List<string> { "ECOMILE" },
            ProductGroup = "GIANT",
            Unit = 1
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=2,
            SaleType = SaleType.Ecomile,
            Product = "Ecomile Tyre",
            ItemCategories = new List<string> { "ECOMILE" },
            ProductGroup = "RADIAL G",
            Unit = 1
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=3,
            SaleType = SaleType.Ecomile,
            Product = "Ecomile Tyre",
            ItemCategories = new List<string> { "ECOMILE" },
            ProductGroup = "LCV",
            Unit = 0.5
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=4,
            SaleType = SaleType.Ecomile,
            Product = "Ecomile Tyre",
            ItemCategories = new List<string> { "ECOMILE" },
            ProductGroup = "PASS",
            Unit = 0.33
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=5,
            SaleType = SaleType.Ecomile,
            Product = "Ecomile Tyre",
            ItemCategories = new List<string> { "ECOMILE" },
            ProductGroup = "TRACTOR",
            Unit = 2
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=6,
            SaleType = SaleType.Ecomile,
            Product = "Ecomile Tyre",
            ItemCategories = new List<string> { "ECOMILE" },
            ProductGroup = "OTR",
            Unit = 7.00
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id=7,
            SaleType = SaleType.Ecomile,
            Product = "Ecomile Tyre",
            ItemCategories = new List<string> { "ECOMILE" },
            ProductGroup = "OTR 1",
            Unit = 3.00
        });
        #endregion Ecomile
        #region Exchange Tyres
        products.Add(new ItemCategoryProductGroup
        {
            Id = 1,
            SaleType = SaleType.ExchangeTyre,
            ItemCategories = new List<string> { "CASING" },
            Name = "Old Used Tyres"
        });
        #endregion Exchange Tyres
        #region Intercompany Retd
        products.Add(new ItemCategoryProductGroup
        {
            Id = 1,
            SaleType = SaleType.IcTyre,                
            ItemCategories = new List<string> { "ECOMILE", "RETD", "CASING" },
            ProductGroup = "GIANT",
            Unit = 1
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 2,
            SaleType = SaleType.IcTyre,                
            ItemCategories = new List<string> { "ECOMILE", "RETD", "CASING" },
            ProductGroup = "RADIAL G",
            Unit = 1
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 3,
            SaleType = SaleType.IcTyre,                
            ItemCategories = new List<string> { "ECOMILE", "RETD", "CASING" },
            ProductGroup = "LCV",
            Unit = 0.5
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 4,
            SaleType = SaleType.IcTyre,                
            ItemCategories = new List<string> { "ECOMILE", "RETD", "CASING" },
            ProductGroup = "PASS",
            Unit = 0.33
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 5,
            SaleType = SaleType.IcTyre,                
            ItemCategories = new List<string> { "ECOMILE", "RETD", "CASING" },
            ProductGroup = "TRACTOR",
            Unit = 2
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 6,
            SaleType = SaleType.IcTyre,                
            ItemCategories = new List<string> { "ECOMILE", "RETD", "CASING" },
            ProductGroup = "OTR",
            Unit = 7.00
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 7,
            SaleType = SaleType.IcTyre,                
            ItemCategories = new List<string> { "ECOMILE", "RETD", "CASING" },
            ProductGroup = "OTR 1",
            Unit = 3.00
        });
        #endregion Intercompany Retd
        #region Scrap
        products.Add(new ItemCategoryProductGroup
        {
            Id = 1,
            SaleType = SaleType.Scrap,
            ItemCategories = new List<string> { "SCRAP" },
            ProductGroup = "RUBDUST",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 2,
            SaleType = SaleType.Scrap,
            ItemCategories = new List<string> { "SCRAP" },
            ProductGroup = "RUB SCRAP",
            Items = new List<string> { "RUB SCRP RDL" },
            Name="RUB RDL"
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 3,
            SaleType = SaleType.Scrap,
            ItemCategories = new List<string> { "SCRAP" },
            ProductGroup = "RUB SCRAP",
            Items = new List<string> { "RUB SCRP NYL" },
            Name = "RUB NYL"
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 4,
            SaleType = SaleType.Scrap,
            ItemCategories = new List<string> { "SCRAP" },
            ProductGroup = "RUB SCRAP",
            Items = new List<string> { "RUB SCRP BEED" },
            Name = "RUB BEED"
        });

        #endregion Scrap
        #region Ecoflex
        products.Add(new ItemCategoryProductGroup
        {
            Id = 1,
            SaleType = SaleType.Ecoflex,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "TILES",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 2,
            SaleType = SaleType.Ecoflex,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "PLAYSAFE",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 3,
            SaleType = SaleType.Ecoflex,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "RUNTRACK",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 4,
            SaleType = SaleType.Ecoflex,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "OUTDOORS",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 5,
            SaleType = SaleType.Ecoflex,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "INDOORSP",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 6,
            SaleType = SaleType.Ecoflex,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "CHARGE",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 7,
            SaleType = SaleType.Ecoflex,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "OTHERS",
        });
        #endregion Ecoflex
        #region Intercompany Ecoflex
        products.Add(new ItemCategoryProductGroup
        {
            Id = 1,
            SaleType = SaleType.IcEcoflex,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "TILES",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 2,
            SaleType = SaleType.IcEcoflex,
            ItemCategories = new List<string> { "RAW-MAT" },
            ProductGroup = "EPDM",
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 3,
            SaleType = SaleType.IcEcoflex,
            ItemCategories = new List<string> { "RAW-MAT" },
            ProductGroup = "BINDER",
        });
        #endregion Intercompany Ecoflex
        #region Trading
        products.Add(new ItemCategoryProductGroup
        {
            Id = 1,
            ItemCategories = new List<string> { "TRADE" },
            ProductGroup = "TUBE",
            SaleType = SaleType.FlapTube,
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 2,
            ItemCategories = new List<string> { "TRADE" },
            ProductGroup = "FLAP",
            SaleType = SaleType.FlapTube,
        });
        #endregion Trading
        #region Tread Rubber
        products.Add(new ItemCategoryProductGroup
        {
            Id = 1,
            SaleType = SaleType.TreadRubber,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "TRADE-RUBB",
            Items = new List<string> { "RUBBER-PCP" },
            Name = "Rubber PCP"
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 2,
            SaleType = SaleType.TreadRubber,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "TRADE-RUBB",
            Items = new List<string> { "RUBBER-HOT" },
            Name = "Rubber HOT"
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 3,
            SaleType = SaleType.TreadRubber,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "TRADE-RUBB",
            Items = new List<string> { "RUBBER-PORT" },
            Name = "Rubber OTR"
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 4,
            SaleType = SaleType.TreadRubber,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "TRADE-RUBB",
            Items = new List<string> { "RUBBER-PATTI" },
            Name = "Rubber Patti"
        });
        products.Add(new ItemCategoryProductGroup
        {
            Id = 4,
            SaleType = SaleType.TreadRubber,
            ItemCategories = new List<string> { "FIN-GOODS" },
            ProductGroup = "TRADE-RUBB",
            Items = new List<string> { "RUBBER-BGUM" },
            Name = "Bonding Gum"
        });
        #endregion Tread Rubber
        return products;
    }

    private static DataTable EmptyDataTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(int));
        dt.Rows.Add(0);
        return dt;
    }

    private async Task<List<ReportBankAccount>> GetBankAccountsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        string bankT = scope.GetQualifiedTableName("Bank Account", isShared: false);
        string terrT = scope.GetQualifiedTableName("Territory", isShared: false);
        string areaT = scope.GetQualifiedTableName("Area", isShared: false);
        string teamT = scope.GetQualifiedTableName("Team Salesperson", isShared: false);
        string custT = scope.GetQualifiedTableName("Customer", isShared: false);
        string invT = scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);

        string sql = $@"
        SELECT 
            BA.[No_] AS Code, 
            BA.[Bank Account No_] AS AccountNo,
            BA.[Real Bank Account No_] AS RealBankAccountNo, 
            BA.[IFSC Code] AS IFSCCode
        FROM {bankT} BA
        WHERE BA.[No_] IN (
            SELECT [Bank Ac No] 
            FROM {terrT} 
            WHERE [Code] IN (
                SELECT [Code] 
                FROM {teamT} 
                WHERE [Type] = 6 AND [Team Code] IN (
                    SELECT [Team] 
                    FROM {areaT} 
                    WHERE [Code] IN (
                        SELECT [Area Code] 
                        FROM {custT} 
                        WHERE [No_] IN (
                            SELECT [Sell-to Customer No_] 
                            FROM {invT} 
                            WHERE [No_] IN @nos
                        )
                    )
                )
            )
        )";

        if (p.Nos?.Count > 0)
        {
            return (await scope.RawQueryToArrayAsync<ReportBankAccount>(sql, new { nos = p.Nos }, ct).ConfigureAwait(false)).ToList();
        }
        return new List<ReportBankAccount>();
    }

    private async Task<List<DocumentAddress>> GetDocumentAddressAsync(ITenantScope scope, string type, string[] docNos, CancellationToken ct)
    {
        string headerT = scope.GetQualifiedTableName(type == "Invoice" ? "Sales Invoice Header" : "Sales Cr_Memo Header", isShared: false);
        string stateT = scope.GetQualifiedTableName("State", isShared: true);

        string sql = $@"
        SELECT 
            Header.[No_] AS No, 
            Header.[Ship-to Code] AS ShipToCode,
            Header.[Sell-to Customer Name] AS Name, 
            Header.[Sell-to Customer Name 2] AS Name2,
            Header.[Sell-to Address] AS Address, 
            Header.[Sell-to Address 2] AS Address2, 
            Header.[Sell-to City] AS City, 
            Header.[Sell-to Post Code] AS PostalCode,
            CASE WHEN Header.[GST Registration No_] <> '' THEN Header.[GST Registration No_] ELSE 'Unregistered' END AS GSTNo,
            CASE WHEN Header.[Ship-to GST Registration No_] <> '' THEN Header.[Ship-to GST Registration No_] ELSE 'Unregistered' END AS ShipGSTNo,
            State.[Description] + ' ( ' + State.[State Code (GST Reg_ No_)] + '/' + State.Code + ' ) ' AS State,
            Header.[Ship-to Name] AS ShipName, 
            Header.[Ship-to Name 2] AS ShipName2,
            Header.[Ship-to Address] AS ShipAddress, 
            Header.[Ship-to Address 2] AS ShipAddress2, 
            Header.[Ship-to City] AS ShipCity, 
            Header.[Ship-to Post Code] AS ShipPostalCode,
            State2.[Description] + ' ( ' + State2.[State Code (GST Reg_ No_)] + '/' + State2.Code + ' ) ' AS ShipState
        FROM {headerT} Header
        LEFT JOIN {stateT} State ON State.[Code] = Header.[GST Bill-to State Code]
        LEFT JOIN {stateT} State2 ON State2.[Code] = Header.[GST Ship-to State Code]
        WHERE Header.[No_] IN @nos";

        var records = await scope.RawQueryToArrayAsync<DocumentAddressRecord>(sql, new { nos = docNos }, ct).ConfigureAwait(false);
        var result = new List<DocumentAddress>();

        foreach (var record in records)
        {
            var document = new DocumentAddress { No = record.No };
            string txt = string.Empty;

            AddTextProperly(ref txt, record.Name?.ToUpper() ?? "", " ");
            AddTextProperly(ref txt, record.Name2 ?? "", " ");
            FillTextProperly(document.BillToAddress, txt);
            txt = string.Empty;

            AddTextProperly(ref txt, record.Address ?? "", string.Empty);
            FillTextProperly(document.BillToAddress, txt);
            txt = string.Empty;

            AddTextProperly(ref txt, record.Address2 ?? "", string.Empty);
            FillTextProperly(document.BillToAddress, txt);
            txt = string.Empty;

            AddTextProperly(ref txt, record.City ?? "", " ");
            AddTextProperly(ref txt, record.PostalCode ?? "", " - ");
            FillTextProperly(document.BillToAddress, txt);
            txt = string.Empty;

            AddTextProperly(ref txt, record.State ?? "", string.Empty);
            FillTextProperly(document.BillToAddress, txt);
            txt = string.Empty;

            AddTextProperly(ref txt, record.GSTNo ?? "", "GSTIN No.: ");
            FillTextProperly(document.BillToAddress, txt);

            if (!string.IsNullOrEmpty(record.ShipToCode))
            {
                txt = string.Empty;
                AddTextProperly(ref txt, record.ShipName?.ToUpper() ?? "", " ");
                AddTextProperly(ref txt, record.ShipName2 ?? "", " ");
                FillTextProperly(document.ShipToAddress, txt);
                txt = string.Empty;

                AddTextProperly(ref txt, record.ShipAddress ?? "", string.Empty);
                FillTextProperly(document.ShipToAddress, txt);
                txt = string.Empty;

                AddTextProperly(ref txt, record.ShipAddress2 ?? "", string.Empty);
                FillTextProperly(document.ShipToAddress, txt);
                txt = string.Empty;

                AddTextProperly(ref txt, record.ShipCity ?? "", " ");
                AddTextProperly(ref txt, record.ShipPostalCode ?? "", " - ");
                FillTextProperly(document.ShipToAddress, txt);
                txt = string.Empty;

                AddTextProperly(ref txt, record.ShipState ?? "", string.Empty);
                FillTextProperly(document.ShipToAddress, txt);
                txt = string.Empty;

                AddTextProperly(ref txt, record.ShipGSTNo ?? "", "GSTIN No.: ");
                FillTextProperly(document.ShipToAddress, txt);
            }
            else
            {
                document.ShipToAddress = document.BillToAddress;
            }

            result.Add(document);
        }
        return result;
    }

    private async Task<List<GstComponent>> GetGstComponentsAsync(ITenantScope scope, CancellationToken ct)
    {
        string gstT = scope.GetQualifiedTableName("GST Component", isShared: false);
        string sql = $"SELECT [Code], [Description] AS [Value] FROM {gstT}";
        var records = await scope.RawQueryToArrayAsync<GstComponent>(sql, null, ct).ConfigureAwait(false);
        return records.ToList();
    }

    private static void AddTextProperly(ref string mainText, string newText, string separator)
    {
        
        if (!string.IsNullOrEmpty(separator))
            mainText += separator + newText;
        else
            mainText += newText;
    }

    private static void FillTextProperly(string[] mainText, string newText)
    {
        if (string.IsNullOrWhiteSpace(newText)) return;
        for (int i = 0; i < mainText.Length; i++)
        {
            if (string.IsNullOrEmpty(mainText[i]))
            {
                mainText[i] = newText;
                return;
            }
        }
    }

    private static string NumberToWords(decimal number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));

        string words = "";
        long intPart = (long)Math.Truncate(number);
        int decPart = (int)((number - intPart) * 100);

        if (intPart > 0) words += NumberToWords(intPart);
        if (decPart > 0)
        {
            if (words != "") words += " and ";
            words += NumberToWords(decPart);
        }
        return words;
    }

    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));

        string words = "";

        if ((number / 10000000) > 0)
        {
            words += NumberToWords(number / 10000000) + " Crore ";
            number %= 10000000;
        }

        if ((number / 100000) > 0)
        {
            words += NumberToWords(number / 100000) + " Lakh ";
            number %= 100000;
        }

        if ((number / 1000) > 0)
        {
            words += NumberToWords(number / 1000) + " Thousand ";
            number %= 1000;
        }

        if ((number / 100) > 0)
        {
            words += NumberToWords(number / 100) + " Hundred ";
            number %= 100;
        }

        if (number > 0)
        {
            if (words != "") words += "and ";
            var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            if (number < 20)
                words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0)
                    words += "-" + unitsMap[number % 10];
            }
        }

        return words;
    }

    private static string ToWords(decimal number, string prefix, string suffix)
    {
        return prefix + NumberToWords(number) + " " + suffix;
    }

    public static string ToTitleCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
    }
}
