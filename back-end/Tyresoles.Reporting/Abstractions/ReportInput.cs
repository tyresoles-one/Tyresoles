namespace Tyresoles.Reporting.Abstractions;

/// <summary>
/// Input for report rendering: optional parameters and data sources. Single DTO to avoid per-report boilerplate.
/// </summary>
public sealed class ReportInput
{
    /// <summary>
    /// Report parameters (name → value). Values are converted to string for RDLC.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Parameters { get; init; }

    /// <summary>
    /// Data sources: data set name → data (IEnumerable or DataTable). Must match names expected by the RDLC.
    /// </summary>
    public IReadOnlyDictionary<string, object>? DataSources { get; init; }
}
