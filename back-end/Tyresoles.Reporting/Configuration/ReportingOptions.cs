namespace Tyresoles.Reporting.Configuration;

/// <summary>
/// Configuration for Tyresoles.Reporting. Bind from "Tyresoles:Reporting" or "TyresolesReporting".
/// </summary>
public sealed class ReportingOptions
{
    public const string SectionName = "Tyresoles:Reporting";

    /// <summary>
    /// Directory path for .rdlc files (e.g. "Reports"). When set, reports are loaded from ReportsPath/{reportName}.rdlc.
    /// If not set, reports are loaded from embedded resources (assembly: Tyresoles.Reporting, folder: Reports).
    /// </summary>
    public string? ReportsPath { get; set; }

    /// <summary>
    /// Use embedded resources for report definitions. Default is true when ReportsPath is not set.
    /// </summary>
    public bool UseEmbeddedResources { get; set; } = true;

    /// <summary>
    /// Cache report definitions in memory by report name. Default true.
    /// </summary>
    public bool EnableDefinitionCache { get; set; } = true;

    /// <summary>
    /// Enable short-lived response cache (same report + same parameters). Default false (dynamic data).
    /// </summary>
    public bool EnableResponseCache { get; set; }

    /// <summary>
    /// Response cache TTL in seconds. Used only when EnableResponseCache is true. Default 60.
    /// </summary>
    public int ResponseCacheSeconds { get; set; } = 60;

    /// <summary>
    /// Max concurrent heavy renders (semaphore). 0 = unlimited. Use to avoid memory spikes when ReportViewerCore returns byte[].
    /// </summary>
    public int MaxConcurrentRenders { get; set; }
}
