using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tyresoles.Data;
using Tyresoles.Data.Features.Production;
using Tyresoles.Data.Features.Sales.Dashboard;
using Tyresoles.Data.Features.Sales.Reports;

namespace Tyresoles.Web.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public sealed class DashboardController : ControllerBase
{
    private const string TenantKey = "NavLive";

    /// <summary>Views accepted by <see cref="ProductionReportService"/> Claim Ratios SQL switch.</summary>
    private static readonly string[] ClaimRatiosViews =
    {
        "Product wise",
        "Pattern wise",
        "Make wise",
        "Submake wise",
        "Dealer wise",
        "Salesperson wise",
        "Defect wise",
        "Proc. Market wise",
    };

    private static readonly HashSet<string> ClaimRatiosViewSet =
        new(ClaimRatiosViews, StringComparer.Ordinal);

    private readonly IDataverseDataService _dataService;
    private readonly ISalesDashboardService _dashboardService;
    private readonly IProductionReportService _productionReportService;

    public DashboardController(
        IDataverseDataService dataService,
        ISalesDashboardService dashboardService,
        IProductionReportService productionReportService)
    {
        _dataService = dataService;
        _dashboardService = dashboardService;
        _productionReportService = productionReportService;
    }

    [HttpPost("{type}")]
    public async Task<IActionResult> GetDashboard(string type, [FromBody] SalesReportParams? param, CancellationToken cancellationToken)
    {
        var p = param ?? new SalesReportParams();
        var scope = _dataService.ForTenant(TenantKey);

        switch (type.ToLowerInvariant())
        {
            case "productsale":
                var productSale = await _dashboardService.GetDashboardSaleAsync(scope, p, cancellationToken);
                return Ok(productSale);
            case "activecustomer":
                var activeCustomer = await _dashboardService.GetDashboardActiveCustomerAsync(scope, p, cancellationToken);
                return Ok(activeCustomer);
            case "dealersale":
                var dealerSale = await _dashboardService.GetDashboardDealerSaleAsync(scope, p, cancellationToken);
                return Ok(dealerSale);
            case "salesmansale":
                var salesmanSale = await _dashboardService.GetDashboardSalesmanSaleAsync(scope, p, cancellationToken);
                return Ok(salesmanSale);
            case "collection":
                var collection = await _dashboardService.GetDashboardCollectionAsync(scope, p, cancellationToken);
                return Ok(collection);
            case "saleschart":
                var chartData = await _dashboardService.GetSalesChartDataAsync(scope, p, cancellationToken);
                return Ok(new { data = chartData });
            case "summary":
                var summary = await _dashboardService.GetDashboardSummaryAsync(scope, p, cancellationToken);
                return Ok(summary);
            case "claimratios":                
                try
                {
                    var claimRows = await _productionReportService.GetClaimRatioDashboardsAsync(scope, p, cancellationToken).ConfigureAwait(false);
                    return Ok(claimRows);
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(new { error = ex.Message, code = "VALIDATION_ERROR" });
                }
            case "procurement":
                try
                {
                    var procurementRows = await _productionReportService.GetProcurementDashboardAsync(scope, p, cancellationToken).ConfigureAwait(false);
                    return Ok(procurementRows);
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(new { error = ex.Message, code = "VALIDATION_ERROR" });
                }
            case "outstanding":
                var outstandingRows = await _dashboardService.GetDashboardOutstandingAsync(scope, p, cancellationToken);
                return Ok(outstandingRows);
            default:
                return BadRequest(new { error = "Unknown dashboard type." });
        }
    }
}
