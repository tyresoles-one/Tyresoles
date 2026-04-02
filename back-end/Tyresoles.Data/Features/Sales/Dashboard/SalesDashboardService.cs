using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Data.Constants;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Data.Features.Sales.Reports.Models;
using Tyresoles.Data.Features.Sales.Reports.Models.Dashboard;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core;
using SalesResult = Tyresoles.Data.Features.Sales.Reports.Models.Dashboard.Sales;
using Tyresoles.Data.Infrastructure;

namespace Tyresoles.Data.Features.Sales.Dashboard
{
    public interface ISalesDashboardService
    {
        Task<DashboardData> GetDashboardSaleAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default);
        Task<DashboardData> GetDashboardActiveCustomerAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default);
        Task<DashboardData> GetDashboardDealerSaleAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default);
        Task<DashboardData> GetDashboardSalesmanSaleAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default);
        Task<DashboardData> GetDashboardCollectionAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default);
        Task<List<MonthlySalesRow>> GetSalesChartDataAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default);
        Task<DashboardSummary> GetDashboardSummaryAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default);
    }

    public class SalesDashboardService : ISalesDashboardService
    {
        private readonly IDataverse _dataverse;
        private readonly GlobalQueryCache _cache;

        public SalesDashboardService(IDataverse dataverse, GlobalQueryCache cache)
        {
            _dataverse = dataverse;
            _cache = cache;
        }

        private string T(ITenantScope scope, string tableName, bool isShared = false)
        {
            // Table names are static enough for a local string cache, but we keep logic consistent
            return scope.GetQualifiedTableName(tableName, isShared);
        }

        // =====================================================================
        // 1. PRODUCT SALE — parallelized across respCenter × product × dateRange
        // =====================================================================
        public async Task<DashboardData> GetDashboardSaleAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default)
        {
            var dashboardData = new DashboardData { Name = "ProductSale" };
            var list = new ConcurrentBag<ProductSale>();

            if (p?.RespCenters?.Count > 0)
            {
                var dateRanges = PrepareDateRanges(p);
                await using var parallel = new ParallelQueryScope(_dataverse, scope.TenantKey, maxParallelism: 8);

                // Build all work items upfront
                var workItems = new List<(string respCenter, ItemCategoryProductGroup prod, List<ItemCategoryProductGroup> products, List<string> glAccounts)>();
                foreach (var respCenter in p.RespCenters)
                {
                    var applicableProds = ApplicableProducts(respCenter).OrderBy(c => c.Id).ToList();
                    if (string.Equals(p.EntityDepartment, Departments.Sales, StringComparison.OrdinalIgnoreCase))
                        applicableProds = applicableProds.Where(c => c.SaleType != SaleType.Scrap && c.SaleType != SaleType.IcTyre && c.SaleType != SaleType.IcEcoflex).ToList();

                    foreach (var applicableProd in applicableProds)
                    {
                        var products = SalesReportService.Products().Where(c => c.SaleType == applicableProd.SaleType).OrderBy(c => c.Id).ToList();
                        var glAccounts = SalesReportService.GetGLAccountForSale(applicableProd.SaleType);
                        workItems.Add((respCenter, applicableProd, products, glAccounts));
                    }
                }

                // Execute all respCenter×product combinations in parallel
                var tasks = workItems.Select(work => parallel.RunAsync(async s =>
                {
                    var productSale = new ProductSale();
                    FillBusinessForRespCenter(productSale, work.respCenter);
                    productSale.Product = work.prod.Product;
                    productSale.ProductId = work.prod.Id;
                    bool isIntercom = work.prod.SaleType == SaleType.IcTyre || work.prod.SaleType == SaleType.IcEcoflex || work.prod.SaleType == SaleType.TreadRubber;

                    if (dateRanges.Count > 0)
                    {
                        int index = 0;
                        // Process date ranges sequentially within each parallel task (they share one scope/connection)
                        foreach (var dateRange in dateRanges)
                        {
                            index++;
                            var newParam = new SalesReportParams
                            {
                                RespCenters = new List<string> { work.respCenter },
                                From = dateRange.From.ToString("dd-MMM-yy", CultureInfo.InvariantCulture),
                                To = dateRange.To.ToString("dd-MMM-yy", CultureInfo.InvariantCulture),
                                EntityCode = p.EntityCode, EntityType = p.EntityType, EntityDepartment = p.EntityDepartment,
                                Type = work.prod.SaleType == SaleType.ExchangeTyre ? "Purchase" : "Sales"
                            };

                            var sales = new SalesResult { Items = new List<Item>(), No = index, Label = dateRange.Label };
                            sales.DateRange = $"({dateRange.From:dd-MMM-yy} .. {dateRange.To:dd-MMM-yy})";
                            sales.Sale = await GetProductSaleAsync(s, newParam, work.glAccounts, cancellationToken);                           
                            sales.SaleText = sales.Sale > 0 ? Math.Round(sales.Sale / 100000, 2).ToString("N2") : "-";
                            sales.SaleUnit = "Lakh";                           

                            var totItem = new Item { Label = "TOTAL", No = 100 };
                            foreach (var product in work.products.OrderBy(c=>c.Id))
                            {
                                var item = await GetItemSoldAsync(s, newParam, product, isIntercom, cancellationToken);
                                totItem.Value += item.Value;
                                sales.Items.Add(item);
                            }
                            totItem.Value = Math.Round(totItem.Value);
                            sales.Items.Add(totItem);
                            productSale.Data.Add(sales);
                        }
                    }
                    list.Add(productSale);
                    return 0; // dummy return for RunAsync<T>
                }, cancellationToken));

                await Task.WhenAll(tasks);
            }

            dashboardData.Data = list.OrderBy(c => c.No).ThenBy(c => c.ProductId).ToList();
            return dashboardData;
        }

        // =====================================================================
        // 2. ACTIVE CUSTOMER — parallelized across respCenter × product
        // =====================================================================
        public async Task<DashboardData> GetDashboardActiveCustomerAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default)
        {
            var dashboardData = new DashboardData { Name = "ActiveCustomer" };
            var customers = new ConcurrentBag<ActiveCustomer>();

            if (p?.RespCenters?.Count > 0)
            {
                var dateRanges = PrepareDateRanges(p);
                await using var parallel = new ParallelQueryScope(_dataverse, scope.TenantKey, maxParallelism: 8);

                var workItems = new List<(string respCenter, ItemCategoryProductGroup prod, List<ItemCategoryProductGroup> products, List<string> glAccounts, List<string> genBusGroups)>();
                foreach (var respCenter in p.RespCenters)
                {
                    var applicableProds = ApplicableProducts(respCenter).OrderBy(c => c.Id).ToList();
                    foreach (var applicableProd in applicableProds)
                    {
                        var products = SalesReportService.Products().Where(c => c.SaleType == applicableProd.SaleType).OrderBy(c => c.Id).ToList();
                        var glAccounts = SalesReportService.GetGLAccountForSale(applicableProd.SaleType);
                        var genBusGroups = SalesReportService.GetGenBusPostingGroupForSale(applicableProd.SaleType);
                        workItems.Add((respCenter, applicableProd, products, glAccounts, genBusGroups));
                    }
                }

                var tasks = workItems.Select(work => parallel.RunAsync(async s =>
                {
                var customer = new ActiveCustomer { Product = work.prod.Product, ProductId = work.prod.Id, Data = new List<CustomerSales>() };
                bool isIntercom = work.prod.SaleType == SaleType.IcTyre || work.prod.SaleType == SaleType.IcEcoflex || work.prod.SaleType == SaleType.TreadRubber;

                foreach (var dateRange in dateRanges)
                {
                    var newParam = new SalesReportParams
                    {
                        RespCenters = new List<string> { work.respCenter },
                        From = dateRange.From.ToString("dd-MMM-yy", CultureInfo.InvariantCulture),
                        To = dateRange.To.ToString("dd-MMM-yy", CultureInfo.InvariantCulture),
                        EntityCode = p.EntityCode, EntityDepartment = p.EntityDepartment, EntityType = p.EntityType
                    };

                    var actCustResult = await GetActCustSaleBalanceAsync(s, newParam, work.glAccounts, work.genBusGroups, cancellationToken);
                    var customerSaleBalances = actCustResult.Records;
                    var totalSale = actCustResult.TotalSale;

                    decimal total = 0; string unit = string.Empty;
                    foreach (var product in work.products)
                    {
                        var item = await GetItemSoldAsync(s, newParam, product, isIntercom, cancellationToken);
                        total += item.Value;
                        if (!string.IsNullOrEmpty(item.Unit)) unit = item.Unit;
                    }

                        var customerSales = new CustomerSales
                        {
                            DateRange = $"({dateRange.From:dd-MMM-yy} .. {dateRange.To:dd-MMM-yy})",
                            Records = customerSaleBalances,
                            Lines = new List<SaleLine>
                            {
                                new SaleLine {
                                    Amount = (totalSale > 0 && customerSaleBalances.Count > 0) ?
                                         (totalSale / 100000m / customerSaleBalances.Count) : 0,
                                    Description = "Sales per Customer",
                                    Unit = "Lakhs"
                                },
                                new SaleLine { Amount = (total > 0 && customerSaleBalances.Count > 0) ? Math.Round(total / customerSaleBalances.Count) : 0, Description = "Unit sold per Customer", Unit = unit }
                            }
                        };
                        customer.Data.Add(customerSales);
                        FillBusinessForRespCenter(customer, work.respCenter);
                    }
                    customers.Add(customer);
                    return 0;
                }, cancellationToken));

                await Task.WhenAll(tasks);
            }

            dashboardData.Data = customers.OrderBy(c => c.No).ThenBy(c => c.ProductId).ToList();
            return dashboardData;
        }

        // =====================================================================
        // 3. DEALER SALE — parallelized across respCenter × product
        // =====================================================================
        public async Task<DashboardData> GetDashboardDealerSaleAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default)
        {
            var dashboardData = new DashboardData { Name = "DealerSale" };
            var list = new ConcurrentBag<SalesmanSale>();

            if (p?.RespCenters?.Count > 0)
            {
                await using var parallel = new ParallelQueryScope(_dataverse, scope.TenantKey, maxParallelism: 8);

                var workItems = new List<(string respCenter, ItemCategoryProductGroup prod, List<ItemCategoryProductGroup> products)>();
                foreach (var respCenter in p.RespCenters)
                {
                    var applicableProds = ApplicableProducts(respCenter).OrderBy(c => c.Id)
                        .Where(c => c.SaleType != SaleType.IcTyre && c.SaleType != SaleType.IcEcoflex && c.SaleType != SaleType.Scrap).ToList();
                    foreach (var applicableProd in applicableProds)
                    {
                        var products = SalesReportService.Products().Where(c => c.SaleType == applicableProd.SaleType).OrderBy(c => c.Id).ToList();
                        workItems.Add((respCenter, applicableProd, products));
                    }
                }

                var tasks = workItems.Select(work => parallel.RunAsync(async s =>
                {
                    var salesmanSale = new SalesmanSale { Product = work.prod.Product, ProductId = work.prod.Id };
                    FillBusinessForRespCenter(salesmanSale, work.respCenter);
                    var records = await GetCustomerSaleBalancesAsync(s, p, work.prod, work.products, work.respCenter, true, cancellationToken);
                    BuildDealerSaleData(salesmanSale, records, work.prod.SaleType);
                    list.Add(salesmanSale);
                    return 0;
                }, cancellationToken));

                await Task.WhenAll(tasks);
            }

            dashboardData.Data = list.OrderBy(c => c.No).ThenBy(c => c.ProductId).ToList();
            return dashboardData;
        }

        // =====================================================================
        // 4. SALESMAN SALE — parallelized across respCenter × product
        // =====================================================================
        public async Task<DashboardData> GetDashboardSalesmanSaleAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default)
        {
            var dashboardData = new DashboardData { Name = "SalesmanSale" };
            var list = new ConcurrentBag<SalesmanSale>();

            if (p?.RespCenters?.Count > 0)
            {
                await using var parallel = new ParallelQueryScope(_dataverse, scope.TenantKey, maxParallelism: 8);

                var workItems = new List<(string respCenter, ItemCategoryProductGroup prod, List<ItemCategoryProductGroup> products)>();
                foreach (var respCenter in p.RespCenters)
                {
                    var applicableProds = ApplicableProducts(respCenter).OrderBy(c => c.Id)
                        .Where(c => c.SaleType != SaleType.IcTyre && c.SaleType != SaleType.IcEcoflex && c.SaleType != SaleType.Scrap).ToList();
                    foreach (var applicableProd in applicableProds)
                    {
                        var products = SalesReportService.Products().Where(c => c.SaleType == applicableProd.SaleType).OrderBy(c => c.Id).ToList();
                        workItems.Add((respCenter, applicableProd, products));
                    }
                }

                var tasks = workItems.Select(work => parallel.RunAsync(async s =>
                {
                    var salesmanSale = new SalesmanSale { Product = work.prod.Product, ProductId = work.prod.Id };
                    FillBusinessForRespCenter(salesmanSale, work.respCenter);
                    var records = await GetCustomerSaleBalancesAsync(s, p, work.prod, work.products, work.respCenter, false, cancellationToken);
                    BuildSalesmanSaleData(salesmanSale, records, work.prod.SaleType);
                    list.Add(salesmanSale);
                    return 0;
                }, cancellationToken));

                await Task.WhenAll(tasks);
            }

            dashboardData.Data = list.OrderBy(c => c.No).ThenBy(c => c.ProductId).ToList();
            return dashboardData;
        }

        // =====================================================================
        // 5. COLLECTION — parallelize today vs period per respCenter
        // =====================================================================
        public async Task<DashboardData> GetDashboardCollectionAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default)
        {
            var dashboardData = new DashboardData { Name = "Collection" };
            var list = new ConcurrentBag<CollectionData>();
            
            DateTime today;
            if (DateTime.TryParse(p.WorkDate, out var wd))
            {
                today = wd.Date;
            }
            else
            {
                today = DateTime.Now.Date;
                if (DateTime.Now.Hour <= 14) today = today.AddDays(-1);
            }
            if (!string.IsNullOrEmpty(p.To) && DateTime.TryParse(p.To, out var dtTo) && dtTo < today)
                today = dtTo;

            if (p.RespCenters?.Count > 0)
            {
                await using var parallel = new ParallelQueryScope(_dataverse, scope.TenantKey, maxParallelism: 8);

                // Each respCenter spawns 2 parallel queries: today + period
                var tasks = p.RespCenters.SelectMany(respCenter =>
                {
                    var todayParam = new SalesReportParams
                    {
                        RespCenters = new List<string> { respCenter },
                        From = today.ToString("dd-MMM-yyyy"), To = today.ToString("dd-MMM-yyyy"),
                        WorkDate = p.WorkDate,
                        EntityCode = p.EntityCode, EntityDepartment = p.EntityDepartment, EntityType = p.EntityType,
                        Areas = p.Areas, Customers = p.Customers, Regions = p.Regions
                    };
                    var periodParam = new SalesReportParams
                    {
                        RespCenters = new List<string> { respCenter },
                        From = new DateTime(today.Year, today.Month, 1).ToString("dd-MMM-yyyy"), To = today.ToString("dd-MMM-yyyy"),
                        WorkDate = p.WorkDate,
                        EntityCode = p.EntityCode, EntityDepartment = p.EntityDepartment, EntityType = p.EntityType,
                        Areas = p.Areas, Customers = p.Customers, Regions = p.Regions
                    };

                    return new[]
                    {
                        parallel.RunAsync(async s =>
                        {
                            var todaysCollections = await GetCollectionRecordsAsync(s, todayParam, cancellationToken);
                            var coll = new CollectionData { Collection = -todaysCollections.Sum(c => c.Amt), Data = todaysCollections, Period = today.ToString("dd-MMM-yy") };
                            FillBusinessForRespCenter(coll, respCenter);
                            list.Add(coll);
                            return 0;
                        }, cancellationToken),
                        parallel.RunAsync(async s =>
                        {
                            DateTime dFrom = DateTime.TryParse(p.From, out var df) ? df.Date : today;
                            DateTime dTo = DateTime.TryParse(p.To, out var dt) ? dt.Date : today;
                            var periodCollections = await GetCollectionRecordsAsync(s, periodParam, cancellationToken);
                            var coll = new CollectionData { Collection = -periodCollections.Sum(c => c.Amt), Data = periodCollections, Period = $"({dFrom:dd-MMM-yy} .. {dTo:dd-MMM-yy})" };
                            FillBusinessForRespCenter(coll, respCenter);
                            list.Add(coll);
                            return 0;
                        }, cancellationToken)
                    };
                });

                await Task.WhenAll(tasks);
            }

            dashboardData.Data = list.OrderBy(c => c.No).ToList();
            return dashboardData;
        }

        // =====================================================================
        // 6. SALES CHART — Monthly aggregated (last 6 months)
        // =====================================================================
        public async Task<List<MonthlySalesRow>> GetSalesChartDataAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default)
        {
            var today = DateTime.TryParse(p.WorkDate, out var wd) ? wd.Date : DateTime.Today;
            // Default to last 6 months based on workdate/today or provided 'To' date
            DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : today;
            DateTime fromDt = new DateTime(toDt.Year, toDt.Month, 1).AddMonths(-6);

            var salesGLs = new List<string>();
            foreach (SaleType st in Enum.GetValues(typeof(SaleType)))
            {
                if (st == SaleType.ExchangeTyre || st == SaleType.IcEcoflex || st == SaleType.IcTyre || st ==SaleType.TreadRubber) continue;
                var gls = SalesReportService.GetGLAccountForSale(st);
                if (gls != null) salesGLs.AddRange(gls);
            }
            salesGLs = salesGLs.Distinct().ToList();

            if (salesGLs.Count == 0) return new List<MonthlySalesRow>();

            string glEntryT = T(scope, "G_L Entry", false);
            string custT = T(scope, "Customer", false);
            string areaT = T(scope, "Area", false);
            string teamT = T(scope, "Team Salesperson", false);

            var param = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) 
            { 
                ["from"] = fromDt, 
                ["to"] = toDt, 
                ["gls"] = salesGLs 
            };

            var where = new List<string> 
            { 
                "GLEntry.[Posting Date] BETWEEN @from AND @to",
                "GLEntry.[G_L Account No_] IN @gls"
            };

            string joinClause = "";
            if (p.RespCenters?.Count > 0)
            {
                param["resps"] = p.RespCenters;
                where.Add("GLEntry.[Responsibility Center] IN @resps");
            }

            if (!string.IsNullOrEmpty(p.EntityCode))
            {
                param["entCode"] = p.EntityCode;
                if (p.EntityType == EntityTypes.Partner)
                {
                    joinClause = $" JOIN {custT} Cust WITH (NOLOCK) ON Cust.[No_] = GLEntry.[Source No_]";
                    where.Add("Cust.[Dealer Code] = @entCode");
                }
                else if (p.EntityType == EntityTypes.PartnerGroup)
                {
                    string salespersonT = T(scope, "Salesperson_Purchaser", false);
                    joinClause = $" JOIN {custT} Cust WITH (NOLOCK) ON Cust.[No_] = GLEntry.[Source No_]";
                    where.Add($"Cust.[Dealer Code] IN (SELECT [Code] FROM {salespersonT} WITH (NOLOCK) WHERE [Group] = @entCode)");
                }
                else if (p.EntityType == EntityTypes.Employee && p.EntityDepartment == Departments.Sales)
                {
                    joinClause = $" JOIN {custT} Cust WITH (NOLOCK) ON Cust.[No_] = GLEntry.[Source No_]";
                    where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @entCode)");
                }
            }

            // High performance query grouped by month
            // Using standard SQL grouping for maximum reliability
            string sql = $@"
                SELECT 
                    FORMAT(GLEntry.[Posting Date], 'MMM-yy') as Month,
                    ISNULL(-SUM(GLEntry.[Amount]) / 100000.0, 0) as Sale
                FROM {glEntryT} GLEntry WITH (NOLOCK)
                {joinClause}
                WHERE {string.Join(" AND ", where)}
                GROUP BY FORMAT(GLEntry.[Posting Date], 'MMM-yy'), YEAR(GLEntry.[Posting Date]), MONTH(GLEntry.[Posting Date])
                ORDER BY YEAR(GLEntry.[Posting Date]), MONTH(GLEntry.[Posting Date])";

            var result = await scope.RawQueryToArrayAsync<MonthlySalesRow>(sql, param, cancellationToken);
            var dataset = result?.ToList() ?? new List<MonthlySalesRow>();
            
            // If any month crosses 50 lakhs (SQL already divided by 100,000, so check for 50)
            bool cross50 = dataset.Any(r => r.Sale >= 50);
            foreach (var row in dataset)
            {
                if (cross50)
                {
                    row.Sale /= 100m; // Convert Lakhs to Crores
                    row.Unit = "Cr";
                }
                else
                {
                    row.Unit = "L";
                }
            }
            return dataset;
        }

        // =====================================================================
        // 7. DASHBOARD SUMMARY — for top-level tiles (Sales, Collections)
        // =====================================================================
        public async Task<DashboardSummary> GetDashboardSummaryAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default)
        {
            var today = DateTime.TryParse(p.WorkDate, out var wd) ? wd.Date : DateTime.Today;
            string cacheKey = $"dash_summary:{scope.TenantKey}:{p.EntityCode}:{p.EntityType}:{today:yyyyMMdd}:{string.Join(",", p.RespCenters ?? new List<string>())}";

            return await _cache.GetOrAddAsync(cacheKey, async () => 
            {
                var summary = new DashboardSummary();
            
            // Try to use provided dates or default to this month relative to today (or workDate)
            DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : new DateTime(today.Year, today.Month, 1);
            DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : today;

            // Trend base: Previous same-duration period
            var duration = (toDt - fromDt).Days + 1;
            var prevFrom = fromDt.AddDays(-duration);
            var prevTo = fromDt.AddDays(-1);

            // Sales GLs
            var salesGLs = new List<string>();
            foreach (SaleType st in Enum.GetValues(typeof(SaleType)))
            {
                if (st == SaleType.ExchangeTyre || st == SaleType.IcEcoflex || st == SaleType.IcTyre || st == SaleType.TreadRubber) continue;
                salesGLs.AddRange(SalesReportService.GetGLAccountForSale(st));                
            }
            salesGLs = salesGLs.Distinct().ToList();
            
            // Fetch current totals
            var curSales = await GetAggSumAsync(scope, p, fromDt, toDt, salesGLs, "Sales", cancellationToken);
            var curColl = await GetAggSumAsync(scope, p, fromDt, toDt, null, "Collection", cancellationToken);
            var curCust = await GetAggCountAsync(scope, p, fromDt, toDt, salesGLs, cancellationToken);

            // Fetch previous totals for trend
            var oldSales = await GetAggSumAsync(scope, p, prevFrom, prevTo, salesGLs, "Sales", cancellationToken);
            var oldColl = await GetAggSumAsync(scope, p, prevFrom, prevTo, null, "Collection", cancellationToken);
            var oldCust = await GetAggCountAsync(scope, p, prevFrom, prevTo, salesGLs, cancellationToken);

            summary.Tiles.Add(new SummaryTile
            {
                Label = "Monthly Sales",
                Value = FormatCr(curSales),
                Trend = CalcTrend(curSales, oldSales),
                Icon = "trending-up",
                Color = "bg-emerald-500/10 text-emerald-600"
            });

            summary.Tiles.Add(new SummaryTile
            {
                Label = "Collections",
                Value = FormatCr(curColl),
                Trend = CalcTrend(curColl, oldColl),
                Icon = "banknote",
                Color = "bg-blue-500/10 text-blue-600"
            });

            summary.Tiles.Add(new SummaryTile
            {
                Label = "Active Customers",
                Value = curCust.ToString("N0"),
                Trend = CalcTrend(curCust, oldCust),
                Icon = "users",
                Color = "bg-indigo-500/10 text-indigo-600"
            });

            return summary;
            }, TimeSpan.FromMinutes(10));
        }

        private async Task<int> GetAggCountAsync(ITenantScope scope, SalesReportParams p, DateTime from, DateTime to, List<string> gls, CancellationToken ct)
        {
            var param = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) 
            { 
                ["from"] = from, 
                ["to"] = to,
                ["entCode"] = (p.EntityCode ?? "").Trim()
            };
            var where = new List<string> { "GLEntry.[Posting Date] BETWEEN @from AND @to", "GLEntry.[Source No_] <> ''" };
            if (gls?.Count > 0) { param["gls"] = gls; where.Add("GLEntry.[G_L Account No_] IN @gls"); }

            string tableName = T(scope, "G_L Entry", false);
            string custT = T(scope, "Customer", false);
            string salespersonT = T(scope, "Salesperson_Purchaser", false);
            string areaT = T(scope, "Area", false);
            string teamT = T(scope, "Team Salesperson", false);

            string joinClause = $" LEFT JOIN {custT} Cust WITH (NOLOCK) ON Cust.[No_] = GLEntry.[Source No_]";
            string entityWhere = "";

            if (!string.IsNullOrEmpty(p.EntityCode))
            {
                if (p.EntityType == EntityTypes.Partner) entityWhere = "Cust.[Dealer Code] = @entCode";
                else if (p.EntityType == EntityTypes.PartnerGroup) entityWhere = $"Cust.[Dealer Code] IN (SELECT [Code] FROM {salespersonT} WITH (NOLOCK) WHERE [Group] = @entCode)";
                else if (p.EntityType == EntityTypes.Employee && p.EntityDepartment == Departments.Sales) entityWhere = $"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @entCode)";                
            }

            if (p.RespCenters?.Count > 0)
            {
                param["resps"] = p.RespCenters;
                where.Add("GLEntry.[Responsibility Center] IN @resps");
            }

            if (p.Areas?.Count > 0) { param["areas"] = p.Areas; where.Add("Cust.[Area Code] IN @areas"); }
            {
                var custNos = SalesReportParams.ParseCustomerNos(p.Customers);
                if (custNos.Length > 0) { param["customers"] = custNos; where.Add("Cust.[No_] IN @customers"); }
            }
            if (p.Regions?.Count > 0) { param["regions"] = p.Regions; where.Add("Cust.[Territory Code] IN @regions"); }

            if (!string.IsNullOrEmpty(entityWhere)) where.Add(entityWhere);

            string sql = $"SELECT COUNT(DISTINCT GLEntry.[Source No_]) FROM {tableName} GLEntry WITH (NOLOCK) {joinClause} WHERE {string.Join(" AND ", where)}";
            return await scope.ExecuteScalarAsync<int>(sql, param, ct).ConfigureAwait(false);
        }

        private async Task<decimal> GetAggSumAsync(ITenantScope scope, SalesReportParams p, DateTime from, DateTime to, List<string>? gls, string type, CancellationToken ct)
        {
            var param = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) 
            { 
                ["from"] = from, 
                ["to"] = to,
                ["entCode"] = (p.EntityCode ?? "").Trim()
            };
            var where = new List<string> { "GLEntry.[Posting Date] BETWEEN @from AND @to" };

            string tableName = type == "Sales" ? T(scope, "G_L Entry", false) : T(scope, "Detailed Cust_ Ledg_ Entry", false);
            bool isSales = type == "Sales";
            string custJoinKey = isSales ? "Source No_" : "Customer No_";
            
            if (isSales)
            {
                if (gls?.Count > 0) { param["gls"] = gls; where.Add("GLEntry.[G_L Account No_] IN @gls"); }
                where.Add("GLEntry.[Source No_] <> ''");
            }
            else
            {
                where.Add("GLEntry.[Document Type] IN (1, 6)"); // Payment, Refund
                where.Add("GLEntry.[Journal Batch Name] <> ''");
                where.Add("GLEntry.[Customer No_] <> ''");
            }

            string custT = T(scope, "Customer", false);
            string salespersonT = T(scope, "Salesperson_Purchaser", false);
            string areaT = T(scope, "Area", false);
            string teamT = T(scope, "Team Salesperson", false);

            string joinClause = $" LEFT JOIN {custT} Cust WITH (NOLOCK) ON Cust.[No_] = GLEntry.[{custJoinKey}]";
            string entityWhere = "";

            if (!string.IsNullOrEmpty(p.EntityCode))
            {
                if (p.EntityType == EntityTypes.Partner)
                {
                    entityWhere = "Cust.[Dealer Code] = @entCode";
                }
                else if (p.EntityType == EntityTypes.PartnerGroup)
                {
                    entityWhere = $"Cust.[Dealer Code] IN (SELECT [Code] FROM {salespersonT} WITH (NOLOCK) WHERE [Group] = @entCode)";
                }
                else if (p.EntityType == EntityTypes.Employee && p.EntityDepartment == Departments.Sales)
                {
                    entityWhere = $"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @entCode)";
                }
            }

            if (p.RespCenters?.Count > 0)
            {
                param["resps"] = p.RespCenters;
                // For G_L entries, use the entry's responsibility center
                // For Detailed Cust Ledg entries, use the Customer table's responsibility center (common in Tyresoles)
                if (isSales) where.Add("GLEntry.[Responsibility Center] IN @resps");
                else where.Add("Cust.[Responsibility Center] IN @resps");
            }

            if (p.Areas?.Count > 0) { param["areas"] = p.Areas; where.Add("Cust.[Area Code] IN @areas"); }
            {
                var custNos = SalesReportParams.ParseCustomerNos(p.Customers);
                if (custNos.Length > 0) { param["customers"] = custNos; where.Add("Cust.[No_] IN @customers"); }
            }
            if (p.Regions?.Count > 0) { param["regions"] = p.Regions; where.Add("Cust.[Territory Code] IN @regions"); }

            if (!string.IsNullOrEmpty(entityWhere)) where.Add(entityWhere);

            string sql = $"SELECT ISNULL(-SUM(GLEntry.[Amount]), 0) FROM {tableName} GLEntry WITH (NOLOCK) {joinClause} WHERE {string.Join(" AND ", where)}";
            return await scope.ExecuteScalarAsync<decimal>(sql, param, ct).ConfigureAwait(false);
        }

        private static string FormatCr(decimal val) => val < 5000000 ? $"₹{val / 100000m:N2} L" : $"₹{val / 10000000m:N2} Cr";
        private static string CalcTrend(decimal cur, decimal old)
        {
            if (old == 0) return cur > 0 ? "+100%" : "0%";
            var diff = ((cur - old) / old) * 100;
            return (diff >= 0 ? "+" : "") + diff.ToString("N1") + "%";
        }

        // =====================================================================
        // HELPER METHODS — unchanged logic, just cleaned up
        // =====================================================================

        private static List<DateRange> PrepareDateRanges(SalesReportParams p)
        {
            var ranges = new List<DateRange>();
            DateTime from = DateTime.TryParse(p.From, out var d1) ? d1 : DateTime.MinValue;
            DateTime to = DateTime.TryParse(p.To, out var d2) ? d2 : DateTime.MinValue;
            var days = (to - from).Days + 1;
            var daysInCurrMonth = DateTime.DaysInMonth(from.Year, from.Month);

            ranges.Add(new DateRange
            {
                From = from, To = to,
                Label = days == daysInCurrMonth ? "This Month" : days < daysInCurrMonth ? "This Date Range" : days <= 99 ? "This Quarter" : "This Date Range"
            });

            if (days == daysInCurrMonth)
            {
                var lm = from.AddMonths(-1);
                ranges.Add(new DateRange { From = new DateTime(lm.Year, lm.Month, 1), To = new DateTime(lm.Year, lm.Month, DateTime.DaysInMonth(lm.Year, lm.Month)), Label = "Last Month" });
            }
            else if (days < daysInCurrMonth)
                ranges.Add(new DateRange { From = from.AddMonths(-1), To = to.AddMonths(-1), Label = "Last Date Range" });
            else if (days <= 99)
            {
                var lqFrom = from.AddMonths(-3);
                int quarterMonth = ((lqFrom.Month - 1) / 3) * 3 + 1;
                var qFrom = new DateTime(lqFrom.Year, quarterMonth, 1);
                ranges.Add(new DateRange { From = qFrom, To = qFrom.AddMonths(3).AddDays(-1), Label = "Last Quarter" });
            }
            else
                ranges.Add(new DateRange { From = to.AddDays(-1).AddDays(-days), To = to.AddDays(-1), Label = "Last Date Range" });

            ranges.Add(new DateRange
            {
                From = from.AddYears(-1), To = to.AddYears(-1),
                Label = days == daysInCurrMonth ? "Same Month Last Year" : days < daysInCurrMonth ? "Same Date Range Last Year" : days <= 99 ? "Same Qtr Last Year" : "Same Date Range Last Year"
            });
            return ranges;
        }

        private static IDashboardData FillBusinessForRespCenter(IDashboardData input, string respCenter)
        {
            input.Business = "Tyre Business"; // Default business
            input.Location = respCenter;     // Default location to code

            switch (respCenter?.ToUpper())
            {
                case "BEL": case "BELGAUM": input.Location = "Belgaum"; input.No = 101; break;
                case "JBP": case "JABALPUR": input.Location = "Jabalpur"; input.No = 102; break;
                case "AHM": case "AHMEDABAD": input.Location = "Ahmedabad"; input.No = 103; break;
                case "DEL": case "DELHI": input.Location = "Delhi"; input.No = 104; break;
                case "PUN": case "PUNE": input.Location = "Pune"; input.No = 105; break;
                case "MUM": case "MUMBAI": input.Location = "Mumbai"; input.No = 106; break;
                case "KOC": case "KOCHI": input.Location = "Kochi"; input.No = 107; break;
                case "VJW": case "VIJAYAWADA": input.Location = "Vijayawada"; input.No = 108; break;
                case "RPR": case "RAIPUR": input.Location = "Raipur"; input.No = 109; break;
                case "HO": case "HEADOFFICE": input.Location = "Head Office"; input.No = 100; break;

                case "ECO-B": input.Business = "Ecoflex Business"; input.Location = "Belgaum (ECO)"; input.No = 201; break;
                case "ECO-M": input.Business = "Ecoflex Business"; input.Location = "Mumbai (ECO)"; input.No = 202; break;
                
                case "MAN": case "MANGALORE": input.Business = "Rubber Business"; input.Location = "Mangalore"; input.No = 301; break;
                
                default:
                    if (respCenter?.StartsWith("ECO") == true) input.Business = "Ecoflex Business";
                    break;
            }
            return input;
        }

        private static void BuildDealerSaleData(SalesmanSale salesmanSale, List<CustomerSaleBalanceRaw> records, SaleType saleType)
        {
            switch (saleType)
            {
                case SaleType.Retread: case SaleType.Ecomile:
                {
                    var data = new List<DealerTyreSale>();
                    if (records.Count > 0)
                    {
                        var groupedByNo = records.GroupBy(c => c.No).Select(c => c.First()).ToList();
                        foreach (var dealer in groupedByNo.GroupBy(c => c.Dealer).Select(c => c.First()))
                        {
                            var filtered = groupedByNo.Where(c => c.Dealer == dealer.Dealer).ToList();
                            var record = new DealerTyreSale { Dealer = dealer.Dealer, Customers = filtered.Count, G = filtered.Sum(c => c.G), R = filtered.Sum(c => c.R), L = filtered.Sum(c => c.L), P = filtered.Sum(c => c.P), T = filtered.Sum(c => c.T), O = filtered.Sum(c => c.O), O1 = filtered.Sum(c => c.O1), Sale = filtered.Sum(c => c.Sale).ToString("N0") };
                            record.Total = record.G + record.R + record.L + record.P + record.T + record.O1 + record.O;
                            var bal = filtered.Sum(c => c.Balance); record.Balance = bal < 0 ? (0 - bal).ToString("N0") + " Cr." : bal.ToString("N0") + " Dr.";
                            data.Add(record);
                        }
                    }
                    salesmanSale.Data = data; break;
                }
                case SaleType.FlapTube:
                {
                    var data = new List<DealerTradeSale>();
                    if (records.Count > 0)
                    {
                        var groupedByNo = records.GroupBy(c => c.No).Select(c => c.First()).ToList();
                        foreach (var dealer in groupedByNo.GroupBy(c => c.Dealer).Select(c => c.First()))
                        {
                            var filtered = groupedByNo.Where(c => c.Dealer == dealer.Dealer).ToList();
                            var record = new DealerTradeSale { Dealer = dealer.Dealer, Customers = filtered.Count, Tube = filtered.Sum(c => c.Tube), Flap = filtered.Sum(c => c.Flap), Sale = filtered.Sum(c => c.Sale).ToString("N0") };
                            record.Total = record.Tube + record.Flap;
                            var bal = filtered.Sum(c => c.Balance); record.Balance = bal < 0 ? (0 - bal).ToString("N0") + " Cr." : bal.ToString("N0") + " Dr.";
                            data.Add(record);
                        }
                    }
                    salesmanSale.Data = data; break;
                }
                case SaleType.Ecoflex:
                {
                    var data = new List<DealerEcoflexSale>();
                    if (records.Count > 0)
                    {
                        var groupedByNo = records.GroupBy(c => c.No).Select(c => c.First()).ToList();
                        foreach (var dealer in groupedByNo.GroupBy(c => c.Dealer).Select(c => c.First()))
                        {
                            var filtered = groupedByNo.Where(c => c.Dealer == dealer.Dealer).ToList();
                            var record = new DealerEcoflexSale { Dealer = dealer.Dealer, Customers = filtered.Count, Indoors = filtered.Sum(c => c.Indoors), Outdoors = filtered.Sum(c => c.Outdoors), PlaySafe = filtered.Sum(c => c.PlaySafe), RunTrack = filtered.Sum(c => c.RunTrack), Tile = filtered.Sum(c => c.Tile), Charges = filtered.Sum(c => c.Charges), Other = filtered.Sum(c => c.Other), Sale = filtered.Sum(c => c.Sale).ToString("N0") };
                            record.Total = record.Indoors + record.Outdoors + record.PlaySafe + record.RunTrack + record.Tile + record.Charges + record.Other;
                            var bal = filtered.Sum(c => c.Balance); record.Balance = bal < 0 ? (0 - bal).ToString("N0") + " Cr." : bal.ToString("N0") + " Dr.";
                            data.Add(record);
                        }
                    }
                    salesmanSale.Data = data; break;
                }
            }
        }

        private static void BuildSalesmanSaleData(SalesmanSale salesmanSale, List<CustomerSaleBalanceRaw> records, SaleType saleType)
        {
            switch (saleType)
            {
                case SaleType.Retread: case SaleType.Ecomile:
                {
                    var data = new List<SalesmanTyreSale>();
                    if (records.Count > 0)
                    {
                        var groupedByNo = records.GroupBy(c => c.No).Select(c => c.First()).ToList();
                        foreach (var area in groupedByNo.GroupBy(c => c.Area).Select(c => c.First()))
                        {
                            var filtered = groupedByNo.Where(c => c.Area == area.Area).ToList();
                            var record = new SalesmanTyreSale { Area = area.Area, Salesperson = area.Salesman, Customers = filtered.Count, G = filtered.Sum(c => c.G), R = filtered.Sum(c => c.R), L = filtered.Sum(c => c.L), P = filtered.Sum(c => c.P), T = filtered.Sum(c => c.T), O = filtered.Sum(c => c.O), O1 = filtered.Sum(c => c.O1), Sale = filtered.Sum(c => c.Sale).ToString("N0") };
                            record.Total = record.G + record.R + record.L + record.P + record.T + record.O1 + record.O;
                            var bal = filtered.Sum(c => c.Balance); record.Balance = bal < 0 ? (0 - bal).ToString("N0") + " Cr." : bal.ToString("N0") + " Dr.";
                            data.Add(record);
                        }
                    }
                    salesmanSale.Data = data; break;
                }
                case SaleType.FlapTube:
                {
                    var data = new List<SalesmanTradeSale>();
                    if (records.Count > 0)
                    {
                        var groupedByNo = records.GroupBy(c => c.No).Select(c => c.First()).ToList();
                        foreach (var area in groupedByNo.GroupBy(c => c.Area).Select(c => c.First()))
                        {
                            var filtered = groupedByNo.Where(c => c.Area == area.Area).ToList();
                            var record = new SalesmanTradeSale { Area = area.Area, Salesperson = area.Salesman, Customers = filtered.Count, Tube = filtered.Sum(c => c.Tube), Flap = filtered.Sum(c => c.Flap), Sale = filtered.Sum(c => c.Sale).ToString("N0") };
                            record.Total = record.Tube + record.Flap;
                            var bal = filtered.Sum(c => c.Balance); record.Balance = bal < 0 ? (0 - bal).ToString("N0") + " Cr." : bal.ToString("N0") + " Dr.";
                            data.Add(record);
                        }
                    }
                    salesmanSale.Data = data; break;
                }
                case SaleType.Ecoflex:
                {
                    var data = new List<SalesmanEcoflexSale>();
                    if (records.Count > 0)
                    {
                        var groupedByNo = records.GroupBy(c => c.No).Select(c => c.First()).ToList();
                        foreach (var area in groupedByNo.GroupBy(c => c.Area).Select(c => c.First()))
                        {
                            var filtered = groupedByNo.Where(c => c.Area == area.Area).ToList();
                            var record = new SalesmanEcoflexSale { Area = area.Area, Salesperson = area.Salesman, Customers = filtered.Count, Indoor = filtered.Sum(c => c.Indoors), Outdoor = filtered.Sum(c => c.Outdoors), PlaySafe = filtered.Sum(c => c.PlaySafe), RunTrack = filtered.Sum(c => c.RunTrack), Tile = filtered.Sum(c => c.Tile), Other = filtered.Sum(c => c.Other), Charges = filtered.Sum(c => c.Charges), Sale = filtered.Sum(c => c.Sale).ToString("N0") };
                            record.Total = record.Tile + record.Indoor + record.Outdoor + record.PlaySafe + record.RunTrack + record.Charges + record.Other;
                            var bal = filtered.Sum(c => c.Balance); record.Balance = bal < 0 ? (0 - bal).ToString("N0") + " Cr." : bal.ToString("N0") + " Dr.";
                            data.Add(record);
                        }
                    }
                    salesmanSale.Data = data; break;
                }
            }
        }

        private static List<ItemCategoryProductGroup> ApplicableProducts(string respCenter)
        {
            var products = new List<ItemCategoryProductGroup>();
            switch (respCenter)
            {
                case "BEL": case "JBP":
                    products.Add(new ItemCategoryProductGroup { Id = 1, Product = "Retreading Tyre", SaleType = SaleType.Retread });
                    products.Add(new ItemCategoryProductGroup { Id = 2, Product = "Ecomile Tyre", SaleType = SaleType.Ecomile });
                    products.Add(new ItemCategoryProductGroup { Id = 3, Product = "Exchange Tyres", SaleType = SaleType.ExchangeTyre });
                    products.Add(new ItemCategoryProductGroup { Id = 4, Product = "Intercompany (Ecomile/Casing/Retd)", SaleType = SaleType.IcTyre });
                    products.Add(new ItemCategoryProductGroup { Id = 5, Product = "Scrap Sale", SaleType = SaleType.Scrap });
                    break;
                case "AHM": case "DEL":
                    products.Add(new ItemCategoryProductGroup { Id = 1, Product = "Ecomile Tyre", SaleType = SaleType.Ecomile });
                    products.Add(new ItemCategoryProductGroup { Id = 2, Product = "Retreading Tyre", SaleType = SaleType.Retread });
                    products.Add(new ItemCategoryProductGroup { Id = 3, Product = "Intercompany (Ecomile/Casing/Retd)", SaleType = SaleType.IcTyre });
                    break;
                case "ECO-B": case "ECO-M":
                    products.Add(new ItemCategoryProductGroup { Id = 1, Product = "Sales & Service", SaleType = SaleType.Ecoflex });
                    products.Add(new ItemCategoryProductGroup { Id = 2, Product = "Intercompany", SaleType = SaleType.IcEcoflex });
                    break;
                case "MAN":
                    products.Add(new ItemCategoryProductGroup { Id = 1, Product = "Intercompany", SaleType = SaleType.TreadRubber });
                    break;
            }
            return products;
        }

        // =====================================================================
        // SQL DATA METHODS — logic unchanged, just accepting scope param
        // =====================================================================

        private async Task<decimal> GetProductSaleAsync(ITenantScope scope, SalesReportParams p, List<string> glAccNos, CancellationToken cancellationToken = default)
        {
            DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.MinValue;
            DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.MaxValue;
            string glEntryT = T(scope, "G_L Entry", isShared: false);
            string custT = T(scope, "Customer", isShared: false);
            string areaT = T(scope, "Area", isShared: false);
            string teamT = T(scope, "Team Salesperson", isShared: false);

            var param = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["fromDt"] = fromDt, ["toDt"] = toDt };
            var where = new List<string> { "GLEntry.[Source No_] <> ''", "GLEntry.[Posting Date] BETWEEN @fromDt AND @toDt" };

            if (p.RespCenters?.Count > 0) { param["respCenters"] = p.RespCenters; where.Add("GLEntry.[Responsibility Center] IN @respCenters"); }
            if (glAccNos?.Count > 0) { param["glAccNos"] = glAccNos; where.Add("GLEntry.[G_L Account No_] IN @glAccNos"); }

            bool joinCustomer = false; var custWhere = new List<string>();
            if (p.Dealers?.Count > 0) { param["dealers"] = p.Dealers; custWhere.Add("Cust.[Dealer Code] IN @dealers"); joinCustomer = true; }
            if (p.Areas?.Count > 0) { param["areas"] = p.Areas; custWhere.Add("Cust.[Area Code] IN @areas"); joinCustomer = true; }
            if (p.Regions?.Count > 0) { param["regions"] = p.Regions; custWhere.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Type]=6 AND T.[Code] IN @regions)"); joinCustomer = true; }

            if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
            {
                param["userCode"] = p.EntityCode;
                if (p.EntityType == EntityTypes.Customer) { custWhere.Add("Cust.[No_] = @userCode"); joinCustomer = true; }
                else if (p.EntityType == EntityTypes.Partner) { custWhere.Add("Cust.[Dealer Code] = @userCode"); joinCustomer = true; }
                else if (p.EntityType == EntityTypes.PartnerGroup)
                {
                    string salespersonT = T(scope, "Salesperson_Purchaser", false);
                    custWhere.Add($"Cust.[Dealer Code] IN (SELECT [Code] FROM {salespersonT} WITH (NOLOCK) WHERE [Group] = @userCode)");
                    joinCustomer = true;
                }
                else if (p.EntityType == EntityTypes.Employee && p.EntityDepartment == Departments.Sales) { custWhere.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode)"); joinCustomer = true; }
            }

            string sqlSelect = p.Type == "Purchase" ? "ISNULL(SUM(GLEntry.[Amount]),0)" : "ISNULL(-SUM(GLEntry.[Amount]),0)";
            string sqlJoin = joinCustomer ? $" LEFT JOIN {custT} Cust ON Cust.[No_] = GLEntry.[Source No_]" : "";
            where.AddRange(custWhere);
            string sql = $"SELECT {sqlSelect} AS Value FROM {glEntryT} GLEntry WITH (NOLOCK) {sqlJoin} WHERE {string.Join(" AND ", where)}";

            var result = await scope.RawQueryToArrayAsync<dynamic>(sql, param, cancellationToken);
            if (result.Length > 0 && result[0].Value != null) return Convert.ToDecimal(result[0].Value);
            return 0;
        }

        private async Task<Item> GetItemSoldAsync(ITenantScope scope, SalesReportParams p, ItemCategoryProductGroup itemParam, bool intercom = false, CancellationToken cancellationToken = default)
        {
            DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.MinValue;
            DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.MaxValue;
            string ledgerT = T(scope, "Item Ledger Entry", isShared: false);
            string custT = T(scope, "Customer", isShared: false);
            string itemT = T(scope, "Item", isShared: false);
            string uomT = T(scope, "Unit of Measure", isShared: false);
            string locT = T(scope, "Location", isShared: false);
            string areaT = T(scope, "Area", isShared: false);
            string teamT = T(scope, "Team Salesperson", isShared: false);

            var param = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["fromDt"] = fromDt, ["toDt"] = toDt };
            var where = new List<string> { "Ledger.[Posting Date] BETWEEN @fromDt AND @toDt" };
            int entryType = itemParam.SaleType == SaleType.ExchangeTyre ? 0 : 1;
            param["entryType"] = entryType; where.Add("Ledger.[Entry Type] = @entryType");
            string valueSelect = itemParam.SaleType == SaleType.ExchangeTyre ? "ROUND(SUM(Ledger.[Quantity]), 0)" : "ROUND(-SUM(Ledger.[Quantity]), 0)";

            if (itemParam.ItemCategories?.Count > 0) { param["itemCats"] = itemParam.ItemCategories; where.Add("Ledger.[Item Category Code] IN @itemCats"); }
            if (!string.IsNullOrEmpty(itemParam.ProductGroup)) { param["prodGroup"] = itemParam.ProductGroup; where.Add("Ledger.[Product Group Code] = @prodGroup"); }
            if (itemParam.Items?.Count > 0) { param["items"] = itemParam.Items; where.Add("Ledger.[Item No_] IN @items"); }

            int[] locTypes = itemParam.SaleType switch { SaleType.Retread => new[] { 0, 1, 3, 4 }, SaleType.Ecomile => new[] { 0, 1, 3, 4 }, SaleType.IcTyre => new[] { 0, 1, 3, 4 }, SaleType.ExchangeTyre => new[] { 6 }, SaleType.Scrap => new[] { 4, 6 }, SaleType.IcEcoflex => new[] { 0 }, SaleType.Ecoflex => new[] { 0 }, SaleType.FlapTube => new[] { 0 }, SaleType.TreadRubber => new[] { 0 }, _ => new int[] { } };
            if (locTypes.Length > 0) { param["locTypes"] = locTypes; where.Add($"Ledger.[Location Code] IN (SELECT Code FROM {locT} WHERE Type IN @locTypes)"); }
            if (p.RespCenters?.Count > 0) { param["respCenters"] = p.RespCenters; where.Add($"Ledger.[Location Code] IN (SELECT Code FROM {locT} WHERE [Responsibility Center] IN @respCenters)"); }

            if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
            {
                param["userCode"] = p.EntityCode;
                if (p.EntityType == EntityTypes.Customer) where.Add($"Ledger.[Source No_] IN (SELECT [No_] FROM {custT} WHERE [No_] = @userCode)");
                else if (p.EntityType == EntityTypes.Partner) where.Add($"Ledger.[Source No_] IN (SELECT [No_] FROM {custT} WHERE [Dealer Code] = @userCode)");
                else if (p.EntityType == EntityTypes.PartnerGroup)
                {
                    string salespersonT = T(scope, "Salesperson_Purchaser", false);
                    where.Add($"Ledger.[Source No_] IN (SELECT [No_] FROM {custT} WHERE [Dealer Code] IN (SELECT [Code] FROM {salespersonT} WITH (NOLOCK) WHERE [Group] = @userCode))");
                }
                else if (p.EntityType == EntityTypes.Employee && p.EntityDepartment == Departments.Sales) where.Add($"Ledger.[Source No_] IN (SELECT [No_] FROM {custT} WHERE [Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode))");
            }

            bool joinCustomer = false;
            if (itemParam.SaleType != SaleType.Scrap && itemParam.SaleType != SaleType.ExchangeTyre)
            {
                joinCustomer = true;
                var cc = intercom ? "<>" : "=";
                where.Add($"Cust.[Gen_ Bus_ Posting Group] {cc} 'SALES'");
            }

            string label = !string.IsNullOrEmpty(itemParam.Name) ? itemParam.Name : (!string.IsNullOrEmpty(itemParam.ProductGroup) ? itemParam.ProductGroup : "");
            string joinClause = joinCustomer ? $"INNER JOIN {custT} Cust ON Cust.[No_] = Ledger.[Source No_]" : "";
            string sql = $@"SELECT {itemParam.Id} as No, '{label}' as Label, ISNULL({valueSelect},0) as Value, (SELECT TOP 1 UOM.[Description] FROM {itemT} Items WITH (NOLOCK) LEFT JOIN {uomT} UOM WITH (NOLOCK) ON UOM.[Code] = Items.[Sales Unit of Measure] WHERE Items.[No_] = MAX(Ledger.[Item No_])) as Unit FROM {ledgerT} Ledger WITH (NOLOCK) {joinClause} WHERE {string.Join(" AND ", where)}";

            var result = await scope.RawQueryToArrayAsync<Item>(sql, param, cancellationToken);
            if (result.Length > 0) return result[0];
            return new Item { No = itemParam.Id, Label = label, Value = 0, Unit = "" };
        }

        private async Task<ActCustSaleBalanceResult> GetActCustSaleBalanceAsync(ITenantScope scope, SalesReportParams p, List<string> glAccNos, List<string> genBusGroups, CancellationToken cancellationToken = default)
        {
            DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.MinValue;
            DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.MaxValue;
            string glEntryT = T(scope, "G_L Entry", isShared: false);
            string custT = T(scope, "Customer", isShared: false);
            string detLedgerT = T(scope, "Detailed Cust_ Ledg_ Entry", isShared: false);
            string areaT = T(scope, "Area", isShared: false);
            string teamT = T(scope, "Team Salesperson", isShared: false);

            var param = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["fromDt"] = fromDt, ["toDt"] = toDt };
            var where = new List<string> { "GLEntry.[Posting Date] BETWEEN @fromDt AND @toDt" };
            if (glAccNos?.Count > 0) { param["glAccNos"] = glAccNos; where.Add("GLEntry.[G_L Account No_] IN @glAccNos"); }
            if (genBusGroups?.Count > 0) { param["genBusGroups"] = genBusGroups; where.Add("GLEntry.[Gen_ Bus_ Posting Group] IN @genBusGroups"); }
            if (p.RespCenters?.Count > 0) { param["respCenters"] = p.RespCenters; where.Add("GLEntry.[Responsibility Center] IN @respCenters"); }

            if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
            {
                param["userCode"] = p.EntityCode;
                if (p.EntityType == EntityTypes.Customer) where.Add("Customer.[No_] = @userCode");
                else if (p.EntityType == EntityTypes.Partner) where.Add("Customer.[Dealer Code] = @userCode");
                else if (p.EntityType == EntityTypes.PartnerGroup)
                {
                    string salespersonT = T(scope, "Salesperson_Purchaser", false);
                    where.Add($"Customer.[Dealer Code] IN (SELECT [Code] FROM {salespersonT} WITH (NOLOCK) WHERE [Group] = @userCode)");
                }
                else if (p.EntityType == EntityTypes.Employee && p.EntityDepartment == Departments.Sales) where.Add($"Customer.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode)");
            }

            string sql = $@"SELECT GLEntry.[Source No_] as Code, Customer.[Name] as Name, -SUM(GLEntry.[Amount]) as Sale, ISNULL(Ledger.Amount, 0) as Balance FROM {glEntryT} GLEntry WITH (NOLOCK) LEFT JOIN {custT} Customer WITH (NOLOCK) ON Customer.[No_] = GLEntry.[Source No_] LEFT JOIN (SELECT [Customer No_] as CustomerNo, SUM(Amount) as Amount FROM {detLedgerT} WITH (NOLOCK) WHERE [Posting Date] <= @toDt GROUP BY [Customer No_]) Ledger ON Ledger.CustomerNo = GLEntry.[Source No_] WHERE {string.Join(" AND ", where)} GROUP BY GLEntry.[Source No_], Customer.[Name], Ledger.Amount";

            var records = await scope.RawQueryToArrayAsync<CustomerSaleBalance>(sql, param, cancellationToken);
            decimal totalSale = records.Length > 0 ? records.Sum(r => r.Sale) : 0;
            return new ActCustSaleBalanceResult { Records = records.ToList(), TotalSale = totalSale };
        }

        private async Task<List<CustomerSaleBalanceRaw>> GetCustomerSaleBalancesAsync(ITenantScope scope, SalesReportParams p, ItemCategoryProductGroup applicableProd, List<ItemCategoryProductGroup> products, string respCenter, bool isDealer, CancellationToken cancellationToken = default)
        {
            DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.MinValue;
            DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.MaxValue;
            string custT = T(scope, "Customer", isShared: false);
            string glEntryT = T(scope, "G_L Entry", isShared: false);
            string detLedgerT = T(scope, "Detailed Cust_ Ledg_ Entry", isShared: false);
            string areaT = T(scope, "Area", isShared: false);
            string teamT = T(scope, "Team Salesperson", isShared: false);
            string itemLedgerT = T(scope, "Item Ledger Entry", isShared: false);
            string locT = T(scope, "Location", isShared: false);
            string dealerT = T(scope, "Salesperson_Purchaser", isShared: false);

            var param = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["fromDt"] = fromDt, ["toDt"] = toDt, ["respCenter"] = respCenter };
            string selectClause = $"Cust.[No_] as No, ISNULL(Ledger.Balance,0) as Balance, ISNULL(GLEntry.Sale,0) as Sale";
            if (isDealer) selectClause += ", Dealer.[Dealership Name] as Dealer";
            else selectClause += ", Area.[Name] as Area, Team.[Name] as Salesman";

            var glAccountForSale = SalesReportService.GetGLAccountForSale(applicableProd.SaleType);
            param["glAccNos"] = glAccountForSale;

            string fromClause = $"FROM {custT} Cust WITH (NOLOCK) ";
            string joinClause = $"LEFT JOIN (SELECT [Source No_] as CustomerNo, -SUM(Amount) as Sale FROM {glEntryT} WITH (NOLOCK) WHERE [Posting Date] BETWEEN @fromDt AND @toDt AND [G_L Account No_] IN @glAccNos GROUP BY [Source No_]) GLEntry ON GLEntry.CustomerNo = Cust.[No_] ";
            joinClause += $"LEFT JOIN (SELECT [Customer No_] as CustomerNo, SUM(Amount) as Balance FROM {detLedgerT} WITH (NOLOCK) WHERE [Posting Date] <= @toDt GROUP BY [Customer No_]) Ledger ON Ledger.CustomerNo = Cust.[No_] ";

            if (isDealer) joinClause += $"LEFT JOIN {dealerT} Dealer WITH (NOLOCK) ON Dealer.[Code] = Cust.[Dealer Code] ";
            else { joinClause += $"LEFT JOIN {areaT} Area WITH (NOLOCK) ON Area.[Code] = Cust.[Area Code] "; joinClause += $"LEFT JOIN {teamT} Team WITH (NOLOCK) ON Team.[Team Code] = Area.[Team] AND Team.[Type] = 0 "; }

            for (int i = 0; i < products.Count; i++)
            {
                var prod = products[i]; string alias = $"Ledger{i}";
                int[] locTypes = prod.SaleType switch { SaleType.Retread => new[] { 0, 1, 3 }, SaleType.Ecomile => new[] { 0, 1, 3 }, SaleType.IcTyre => new[] { 0, 1, 3 }, SaleType.Scrap => new[] { 4 }, SaleType.IcEcoflex => new[] { 0 }, SaleType.Ecoflex => new[] { 0 }, SaleType.FlapTube => new[] { 0 }, SaleType.TreadRubber => new[] { 0 }, _ => new int[] { } };
                string locWhere = locTypes.Length > 0 ? (param[$"locTypes{i}"] = locTypes) != null ? $"AND [Location Code] IN (SELECT [Code] FROM {locT} WITH (NOLOCK) WHERE [Responsibility Center] = @respCenter AND [Type] IN @locTypes{i})" : "" : $"AND [Location Code] IN (SELECT [Code] FROM {locT} WITH (NOLOCK) WHERE [Responsibility Center] = @respCenter)";
                param[$"itemCats{i}"] = prod.ItemCategories; param[$"prodGroup{i}"] = prod.ProductGroup ?? "";
                joinClause += $" LEFT JOIN (SELECT [Source No_] as CustomerNo, -SUM(Quantity) as Qty FROM {itemLedgerT} WITH (NOLOCK) WHERE [Entry Type] = 1 AND [Posting Date] BETWEEN @fromDt AND @toDt AND [Item Category Code] IN @itemCats{i} AND [Product Group Code] = @prodGroup{i} {locWhere} GROUP BY [Source No_]) {alias} ON {alias}.CustomerNo = Cust.[No_] ";

                switch (prod.SaleType)
                {
                    case SaleType.Retread: case SaleType.Ecomile:
                        selectClause += prod.ProductGroup == "OTR 1" ? $", ISNULL({alias}.Qty,0) as O1" : $", ISNULL({alias}.Qty,0) as [{prod.ProductGroup?.Substring(0, 1)}]"; break;
                    case SaleType.FlapTube:
                        selectClause += $", ISNULL({alias}.Qty,0) as {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(prod.ProductGroup?.ToLowerInvariant() ?? "")}"; break;
                    case SaleType.Ecoflex:
                        switch (prod.ProductGroup?.Trim())
                        {
                            case "TILES": selectClause += $", ISNULL({alias}.Qty,0) as Tile"; break;
                            case "PLAYSAFE": selectClause += $", ISNULL({alias}.Qty,0) as PlaySafe"; break;
                            case "RUNTRACK": selectClause += $", ISNULL({alias}.Qty,0) as RunTrack"; break;
                            case "OUTDOORS": selectClause += $", ISNULL({alias}.Qty,0) as Outdoors"; break;
                            case "INDOORSP": selectClause += $", ISNULL({alias}.Qty,0) as Indoors"; break;
                            case "OTHERS": selectClause += $", ISNULL({alias}.Qty,0) as Other"; break;
                            case "CHARGE": selectClause += $", ISNULL({alias}.Qty,0) as Charges"; break;
                        }
                        break;
                }
            }

            var where = new List<string> { "ISNULL(GLEntry.Sale,0) <> 0" };
            if (!string.IsNullOrEmpty(respCenter)) where.Add("Cust.[Responsibility Center] = @respCenter");
            if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
            {
                param["userCode"] = p.EntityCode;
                if (p.EntityType == EntityTypes.Customer) where.Add("Cust.[No_] = @userCode");
                else if (p.EntityType == EntityTypes.Partner) where.Add("Cust.[Dealer Code] = @userCode");
                else if (p.EntityType == EntityTypes.PartnerGroup)
                {
                    string salespersonT = T(scope, "Salesperson_Purchaser", false);
                    where.Add($"Cust.[Dealer Code] IN (SELECT [Code] FROM {salespersonT} WITH (NOLOCK) WHERE [Group] = @userCode)");
                }
                else if (p.EntityType == EntityTypes.Employee && p.EntityDepartment == Departments.Sales) where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode)");
            }

            string sql = $"SELECT {selectClause} {fromClause} {joinClause} WHERE {string.Join(" AND ", where)}";
            var result = await scope.RawQueryToArrayAsync<CustomerSaleBalanceRaw>(sql, param, cancellationToken);
            return result.ToList();
        }

        private async Task<List<Collection>> GetCollectionRecordsAsync(ITenantScope scope, SalesReportParams p, CancellationToken cancellationToken = default)
        {
            DateTime fromDt = DateTime.TryParse(p.From, out var fd) ? fd : DateTime.MinValue;
            DateTime toDt = DateTime.TryParse(p.To, out var td) ? td : DateTime.MaxValue;
            string detLedgerT = T(scope, "Detailed Cust_ Ledg_ Entry", isShared: false);
            string custT = T(scope, "Customer", isShared: false);
            string areaT = T(scope, "Area", isShared: false);
            string teamT = T(scope, "Team Salesperson", isShared: false);

            var param = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["fromDt"] = fromDt, ["toDt"] = toDt };
            var where = new List<string> { "Ledger.[Document Type] IN (1, 6)", "Ledger.[Journal Batch Name] <> ''", "Ledger.[Posting Date] BETWEEN @fromDt AND @toDt" };
            if (p.RespCenters?.Count > 0) { param["respCenters"] = p.RespCenters; where.Add("Cust.[Responsibility Center] IN @respCenters"); }
            if (!string.IsNullOrEmpty(p.EntityType) && !string.IsNullOrEmpty(p.EntityCode))
            {
                param["userCode"] = p.EntityCode;
                if (p.EntityType == EntityTypes.Customer) where.Add("Cust.[No_] = @userCode");
                else if (p.EntityType == EntityTypes.Partner) where.Add("Cust.[Dealer Code] = @userCode");
                else if (p.EntityType == EntityTypes.PartnerGroup)
                {
                    string salespersonT = T(scope, "Salesperson_Purchaser", false);
                    where.Add($"Cust.[Dealer Code] IN (SELECT [Code] FROM {salespersonT} WITH (NOLOCK) WHERE [Group] = @userCode)");
                }
                else if (p.EntityType == EntityTypes.Employee && p.EntityDepartment == Departments.Sales) where.Add($"Cust.[Area Code] IN (SELECT A.[Code] FROM {areaT} A JOIN {teamT} T ON A.[Team]=T.[Team Code] WHERE T.[Code] = @userCode)");
            }
            if (p.Areas?.Count > 0) { param["areas"] = p.Areas; where.Add("Cust.[Area Code] IN @areas"); }
            {
                var custNos = SalesReportParams.ParseCustomerNos(p.Customers);
                if (custNos.Length > 0) { param["customers"] = custNos; where.Add("Cust.[No_] IN @customers"); }
            }

            string sql = $@"SELECT FORMAT(Ledger.[Posting Date], 'dd. MMM yy') as Date, IIF(Ledger.[Document Type] = 1, 'Payment', 'Refund') as Type, Cust.[No_] as CustomerNo, Cust.[Name] as Name, IIF(Ledger.Amount < 0, FORMAT(-Ledger.[Amount], 'N2'), FORMAT(Ledger.[Amount], 'N2')) as Amount, Ledger.Amount as Amt FROM {detLedgerT} Ledger WITH (NOLOCK) LEFT JOIN {custT} Cust WITH (NOLOCK) ON Cust.[No_] = Ledger.[Customer No_] WHERE {string.Join(" AND ", where)}";

            var records = await scope.RawQueryToArrayAsync<Collection>(sql, param, cancellationToken);
            return records.ToList();
        }

        private class CustomerSaleBalanceRaw
        {
            public string No { get; set; }
            public string Area { get; set; }
            public string Salesman { get; set; }
            public string Dealer { get; set; }
            public int Customers { get; set; }
            public decimal Sale { get; set; }
            public decimal Balance { get; set; }
            public int G { get; set; }
            public int R { get; set; }
            public int L { get; set; }
            public int P { get; set; }
            public int T { get; set; }
            public int O { get; set; }
            public int O1 { get; set; }
            public int Tube { get; set; }
            public int Flap { get; set; }
            public int Tile { get; set; }
            public int PlaySafe { get; set; }
            public int RunTrack { get; set; }
            public int Outdoors { get; set; }
            public int Indoors { get; set; }
            public int Charges { get; set; }
            public int Other { get; set; }
        }
    }
}
