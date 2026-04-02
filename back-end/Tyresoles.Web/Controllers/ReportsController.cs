using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tyresoles.Data;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Data.Features.Payroll;
using Tyresoles.Data.Features.Production;
using Tyresoles.Data.Features.Accounts.Reports;

namespace Tyresoles.Web.Controllers;

/// <summary>
/// Unified API for report generation. All report types (Sales, Payroll, Account, etc.) are exposed under /api/reports.
/// Payroll and Production use existing <see cref="SalesReportParams"/> (ReportName, From, To, Nos, RespCenters, Regions, Type, View, ReportOutput).
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReportsController : ControllerBase
{
    private const string TenantKey = "NavLive";
    private static readonly string[] SupportedFormats = { "PDF", "EXCEL" };

    private readonly ISalesReportService _salesReportService;
    private readonly IPayrollReportService _payrollReportService;
    private readonly IProductionReportService _productionReportService;
    private readonly IAccountsReportService _accountsReportService;
    private readonly IDataverseDataService _dataService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        ISalesReportService salesReportService,
        IPayrollReportService payrollReportService,
        IProductionReportService productionReportService,
        IAccountsReportService accountsReportService,
        IDataverseDataService dataService,
        ILogger<ReportsController> logger)
    {
        _salesReportService = salesReportService ?? throw new ArgumentNullException(nameof(salesReportService));
        _payrollReportService = payrollReportService ?? throw new ArgumentNullException(nameof(payrollReportService));
        _productionReportService = productionReportService ?? throw new ArgumentNullException(nameof(productionReportService));
        _accountsReportService = accountsReportService ?? throw new ArgumentNullException(nameof(accountsReportService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a sales report (e.g. Posted Sales Invoice, Sales &amp; Balance) and returns it as a file.
    /// </summary>
    /// <param name="parameters">Report name, output format, and filters (dates, customers, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Report file (PDF). 400 if parameters invalid or format not supported; 404 if no data; 500 on server error.</returns>
    [HttpPost("sales")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SalesReports(
        [FromBody] SalesReportParams? parameters,
        CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            _logger.LogWarning("SalesReports called with null body.");
            return BadRequest(new { error = "Request body is required.", code = "REQUIRED_BODY" });
        }

        var reportName = parameters.ReportName?.Trim();
        if (string.IsNullOrEmpty(reportName))
        {
            _logger.LogWarning("SalesReports called with missing or empty ReportName.");
            return BadRequest(new { error = "ReportName is required.", code = "REQUIRED_REPORT_NAME" });
        }

        var output = (parameters.ReportOutput ?? "PDF").Trim().ToUpperInvariant();
        if (!SupportedFormats.Contains(output))
        {
            _logger.LogWarning("SalesReports requested unsupported format: {Format}. Report: {ReportName}.", output, reportName);
            return BadRequest(new
            {
                error = $"Only {string.Join(" and ", SupportedFormats)} output is supported. Requested: {output}.",
                code = "UNSUPPORTED_FORMAT",
                supportedFormats = SupportedFormats
            });
        }

        try
        {
            var scope = _dataService.ForTenant(TenantKey);
            var bytes = await _salesReportService.RenderReportAsync(scope, reportName, parameters, cancellationToken)
                .ConfigureAwait(false);

            if (bytes == null || bytes.Length == 0)
            {
                _logger.LogInformation("Sales report produced no data. Report: {ReportName}.", reportName);
                return NotFound(new { error = "No data available for the report with the given filters.", code = "NO_DATA" });
            }

            var isExcel = output == "EXCEL";
            var contentType = isExcel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf";
            var fileExtension = isExcel ? ".xlsx" : ".pdf";
            var fileName = SanitizeFileName(reportName) + fileExtension;
            return File(bytes, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "SalesReports validation failed. Report: {ReportName}.", reportName);
            return BadRequest(new { error = ex.Message, code = "VALIDATION_ERROR" });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogInformation(ex, "Sales report definition not found. Report: {ReportName}.", reportName);
            return NotFound(new { error = "Report not found.", message = ex.Message, code = "REPORT_NOT_FOUND" });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sales report generation failed. Report: {ReportName}.", reportName);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Report rendering failed.", code = "RENDER_ERROR" });
        }
    }

    /// <summary>
    /// Returns the list of payroll report names (same as legacy PayrollReportNames).
    /// </summary>
    [HttpGet("payroll/names")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetPayrollReportNames()
    {
        return Ok(_payrollReportService.GetReportNames());
    }

    /// <summary>
    /// Returns payroll report metadata (report names and filter options). Aligns with legacy PayrollReportData.
    /// </summary>
    /// <param name="userId">Optional user ID for permission-based report list.</param>
    [HttpGet("payroll/meta")]
    [ProducesResponseType(typeof(List<ReportMeta>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayrollReportMeta(
        [FromQuery] string? reports,
        CancellationToken cancellationToken)
    {
        var scope = _dataService.ForTenant(TenantKey);
        var meta = await _payrollReportService.GetReportMetaAsync(scope, reports, cancellationToken).ConfigureAwait(false);
        return Ok(meta);
    }

    /// <summary>
    /// Generates a payroll report (e.g. Pay Slip, Pay Sheet, Department Summary) and returns it as a file.
    /// Uses existing <see cref="SalesReportParams"/>: ReportName, From, To, Nos, RespCenters, Regions, Type, View, ReportOutput.
    /// </summary>
    [HttpPost("payroll")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PayrollReports(
        [FromBody] SalesReportParams? parameters,
        CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            _logger.LogWarning("PayrollReports called with null body.");
            return BadRequest(new { error = "Request body is required.", code = "REQUIRED_BODY" });
        }

        var reportName = parameters.ReportName?.Trim();
        if (string.IsNullOrEmpty(reportName))
        {
            _logger.LogWarning("PayrollReports called with missing or empty ReportName.");
            return BadRequest(new { error = "ReportName is required.", code = "REQUIRED_REPORT_NAME" });
        }

        var output = (parameters.ReportOutput ?? "PDF").Trim().ToUpperInvariant();
        if (!SupportedFormats.Contains(output))
        {
            return BadRequest(new
            {
                error = $"Only {string.Join(" and ", SupportedFormats)} output is supported. Requested: {output}.",
                code = "UNSUPPORTED_FORMAT",
                supportedFormats = SupportedFormats
            });
        }

        try
        {
            var scope = _dataService.ForTenant(TenantKey);
            var bytes = await _payrollReportService.RenderReportAsync(scope, reportName, parameters, cancellationToken)
                .ConfigureAwait(false);

            if (bytes == null || bytes.Length == 0)
            {
                _logger.LogInformation("Payroll report produced no data. Report: {ReportName}.", reportName);
                return NotFound(new { error = "No data available for the report with the given filters.", code = "NO_DATA" });
            }

            var isExcel = output == "EXCEL";
            var contentType = isExcel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf";
            var fileExtension = isExcel ? ".xlsx" : ".pdf";
            var fileName = SanitizeFileName(reportName) + fileExtension;
            return File(bytes, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "PayrollReports validation failed. Report: {ReportName}.", reportName);
            return BadRequest(new { error = ex.Message, code = "VALIDATION_ERROR" });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payroll report generation failed. Report: {ReportName}.", reportName);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Report rendering failed.", code = "RENDER_ERROR" });
        }
    }

    /// <summary>
    /// Returns the list of production report names.
    /// </summary>
    [HttpGet("production/names")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetProductionReportNames()
    {
        return Ok(_productionReportService.GetReportNames());
    }

    /// <summary>
    /// Returns production report metadata (report names and filter options).
    /// </summary>
    /// <param name="userId">Optional user ID for permission-based report list.</param>
    [HttpGet("production/meta")]
    [ProducesResponseType(typeof(List<ReportMeta>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionReportMeta(
        [FromQuery] string? userId,
        CancellationToken cancellationToken)
    {
        var scope = _dataService.ForTenant(TenantKey);
        var meta = await _productionReportService.GetReportMetaAsync(scope, userId, cancellationToken).ConfigureAwait(false);
        return Ok(meta);
    }

    /// <summary>
    /// Generates a production report (e.g. Ecomile Aging, Exchange Tyres, Vendor Bill) and returns it as a file.
    /// Uses existing <see cref="SalesReportParams"/>: ReportName, From, To, Nos, RespCenters, Regions, Type, View, ReportOutput.
    /// </summary>
    [HttpPost("production")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProductionReports(
        [FromBody] SalesReportParams? parameters,
        CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            _logger.LogWarning("ProductionReports called with null body.");
            return BadRequest(new { error = "Request body is required.", code = "REQUIRED_BODY" });
        }

        var reportName = parameters.ReportName?.Trim();
        if (string.IsNullOrEmpty(reportName))
        {
            _logger.LogWarning("ProductionReports called with missing or empty ReportName.");
            return BadRequest(new { error = "ReportName is required.", code = "REQUIRED_REPORT_NAME" });
        }

        var output = (parameters.ReportOutput ?? "PDF").Trim().ToUpperInvariant();
        if (!SupportedFormats.Contains(output))
        {
            return BadRequest(new
            {
                error = $"Only {string.Join(" and ", SupportedFormats)} output is supported. Requested: {output}.",
                code = "UNSUPPORTED_FORMAT",
                supportedFormats = SupportedFormats
            });
        }

        try
        {
            var scope = _dataService.ForTenant(TenantKey);
            var bytes = await _productionReportService.RenderReportAsync(scope, reportName, parameters, cancellationToken)
                .ConfigureAwait(false);

            if (bytes == null || bytes.Length == 0)
            {
                _logger.LogInformation("Production report produced no data. Report: {ReportName}.", reportName);
                return NotFound(new { error = "No data available for the report with the given filters.", code = "NO_DATA" });
            }

            var isExcel = output == "EXCEL";
            var contentType = isExcel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf";
            var fileExtension = isExcel ? ".xlsx" : ".pdf";
            var fileName = SanitizeFileName(reportName) + fileExtension;
            return File(bytes, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "ProductionReports validation failed. Report: {ReportName}.", reportName);
            return BadRequest(new { error = ex.Message, code = "VALIDATION_ERROR" });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Production report generation failed. Report: {ReportName}.", reportName);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Report rendering failed.", code = "RENDER_ERROR" });
        }
    }

    private static string SanitizeFileName(string reportName)
    {
        if (string.IsNullOrWhiteSpace(reportName)) return "report";
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var segments = reportName.Split(invalid, StringSplitOptions.RemoveEmptyEntries);
        var sanitized = string.Join("_", segments).Trim();
        return string.IsNullOrEmpty(sanitized) ? "report" : sanitized;
    }

    /// <summary>
    /// Returns the list of accounts report names.
    /// </summary>
    [HttpGet("accounts/names")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetAccountsReportNames()
    {
        return Ok(_accountsReportService.GetReportNames());
    }

    /// <summary>
    /// Returns accounts report metadata (report names and filter options).
    /// </summary>
    /// <param name="userId">Optional user ID for permission-based report list.</param>
    [HttpGet("accounts/meta")]
    [ProducesResponseType(typeof(List<ReportMeta>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccountsReportMeta(
        [FromQuery] string? userId,
        CancellationToken cancellationToken)
    {
        var scope = _dataService.ForTenant(TenantKey);
        var meta = await _accountsReportService.GetReportMetaAsync(scope, userId, cancellationToken).ConfigureAwait(false);
        return Ok(meta);
    }

    /// <summary>
    /// Generates an accounts report and returns it as a file.
    /// </summary>
    [HttpPost("accounts")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AccountsReports(
        [FromBody] SalesReportParams? parameters,
        CancellationToken cancellationToken)
    {
        if (parameters == null)
        {
            _logger.LogWarning("AccountsReports called with null body.");
            return BadRequest(new { error = "Request body is required.", code = "REQUIRED_BODY" });
        }

        var reportName = parameters.ReportName?.Trim();
        if (string.IsNullOrEmpty(reportName))
        {
            _logger.LogWarning("AccountsReports called with missing or empty ReportName.");
            return BadRequest(new { error = "ReportName is required.", code = "REQUIRED_REPORT_NAME" });
        }

        var output = (parameters.ReportOutput ?? "PDF").Trim().ToUpperInvariant();
        if (!SupportedFormats.Contains(output))
        {
            return BadRequest(new
            {
                error = $"Only {string.Join(" and ", SupportedFormats)} output is supported. Requested: {output}.",
                code = "UNSUPPORTED_FORMAT",
                supportedFormats = SupportedFormats
            });
        }

        try
        {
            var scope = _dataService.ForTenant(TenantKey);
            var bytes = await _accountsReportService.RenderReportAsync(scope, reportName, parameters, cancellationToken)
                .ConfigureAwait(false);

            if (bytes == null || bytes.Length == 0)
            {
                _logger.LogInformation("Accounts report produced no data. Report: {ReportName}.", reportName);
                return NotFound(new { error = "No data available for the report with the given filters.", code = "NO_DATA" });
            }

            var isExcel = output == "EXCEL";
            var contentType = isExcel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf";
            var fileExtension = isExcel ? ".xlsx" : ".pdf";
            var fileName = SanitizeFileName(reportName) + fileExtension;
            return File(bytes, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "AccountsReports validation failed. Report: {ReportName}.", reportName);
            return BadRequest(new { error = ex.Message, code = "VALIDATION_ERROR" });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accounts report generation failed. Report: {ReportName}.", reportName);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Report rendering failed.", code = "RENDER_ERROR" });
        }
    }

    /// <summary>
    /// Fetches all report metadata for the sales category.
    /// </summary>
    /// <param name="reports">Optional comma-separated list of names to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of report metadata.</returns>
    [HttpGet("sales/meta")]
    [ProducesResponseType(typeof(List<ReportMeta>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesReportMeta(
        [FromQuery] string? reports,
        CancellationToken cancellationToken)
    {
        var scope = _dataService.ForTenant(TenantKey);
        var meta = await _salesReportService.GetReportMetaAsync(scope, reports, cancellationToken).ConfigureAwait(false);
        return Ok(meta);
    }
}
