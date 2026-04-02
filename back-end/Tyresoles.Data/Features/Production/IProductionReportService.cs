using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Production;

/// <summary>
/// Generates production/procurement reports using Tyresoles.Sql for data and Tyresoles.Reporting for rendering.
/// Report names align with legacy Provider (e.g. "Ecomile Aging", "Posted Proc Order", "Casing Average Cost").
/// Uses existing <see cref="SalesReportParams"/> for filters (ReportName, From, To, Nos, RespCenters, Type, View, ReportOutput).
/// </summary>
public interface IProductionReportService
{
    /// <summary>
    /// Renders the production report to PDF (or Excel when ReportOutput is EXCEL) and returns the file bytes.
    /// </summary>
    /// <param name="scope">Tenant scope (e.g. NavLive).</param>
    /// <param name="reportName">Logical report name (e.g. "Ecomile Aging").</param>
    /// <param name="parameters">Report filters; use existing <see cref="SalesReportParams"/> (From, To, Nos, RespCenters, Type, View, ReportOutput).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Report file bytes (PDF or Excel).</returns>
    Task<byte[]> RenderReportAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the list of production report names (same as legacy ProductionReportNames).
    /// </summary>
    IReadOnlyList<string> GetReportNames();

    /// <summary>
    /// Returns report metadata for production. Aligns with legacy ProductionReportData.
    /// </summary>
    /// <param name="scope">Tenant scope.</param>
    /// <param name="userId">Optional user ID for permission-based report list; if null, all reports are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<ReportMeta>> GetReportMetaAsync(
        ITenantScope scope,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
