using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tyresoles.Data;
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
    private readonly IDataverseDataService _dataService;
    private readonly ISalesDashboardService _dashboardService;

    public DashboardController(IDataverseDataService dataService, ISalesDashboardService dashboardService)
    {
        _dataService = dataService;
        _dashboardService = dashboardService;
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
            default:
                return BadRequest(new { error = "Unknown dashboard type." });
        }
    }
}
