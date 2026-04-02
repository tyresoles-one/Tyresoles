using Tyresoles.Data.Features.Sales.Reports.Models;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Sales.Reports;

/// <summary>
/// Generates sales reports using Tyresoles.Sql for data and Tyresoles.Reporting for rendering.
/// Report names align with legacy Provider (e.g. "Posted Sales Invoice", "Sales & Balance").
/// </summary>
public interface ISalesReportService
{
    /// <summary>
    /// Renders the report to PDF (or other format if supported by renderer) and returns the file bytes.
    /// </summary>
    /// <param name="scope">Tenant scope (e.g. NavLive).</param>
    /// <param name="reportName">Logical report name (e.g. "Posted Sales Invoice").</param>
    /// <param name="parameters">Report filters and options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Report file bytes (PDF by default).</returns>
    Task<byte[]> RenderReportAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches documents (Invoices/Credit Memos) based on parameters.
    /// </summary>
    Task<DocumentDto[]> GetMyDocuments(
        ITenantScope scope,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all report metadata from the GroupDetails table with Category "RPT-SALES".
    /// </summary>
    /// <param name="scope">Tenant scope.</param>
    /// <param name="reports">Optional comma-separated list of names to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of report metadata.</returns>
    Task<List<ReportMeta>> GetReportMetaAsync(
        ITenantScope scope,
        string? reports = null,
        CancellationToken cancellationToken = default);
}
