namespace Tyresoles.Reporting.Abstractions;

/// <summary>
/// Renders a report by name to a stream (e.g. PDF). Engine-agnostic contract for the host.
/// </summary>
public interface IReportRenderer
{
    /// <summary>
    /// Renders the report to PDF and returns a stream. Caller is responsible for disposing the stream after use.
    /// </summary>
    /// <param name="reportName">Logical report name (e.g. file name without extension).</param>
    /// <param name="input">Optional parameters and data sources.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the PDF. Do not dispose inside the renderer when returning for Results.Stream.</returns>
    Task<Stream> RenderPdfAsync(string reportName, ReportInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the report to Excel (.xlsx) and returns a stream. Caller is responsible for disposing the stream after use.
    /// </summary>
    /// <param name="reportName">Logical report name (e.g. file name without extension).</param>
    /// <param name="input">Optional parameters and data sources.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the Excel file.</returns>
    Task<Stream> RenderExcelAsync(string reportName, ReportInput input, CancellationToken cancellationToken = default);
}
