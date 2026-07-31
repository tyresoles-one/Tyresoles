using System.Collections.Generic;

namespace Tyresoles.Web.Features.Downloads;

/// <summary>Configuration for showcase downloads list.</summary>
public sealed class DownloadsOptions
{
    public const string SectionName = "Downloads";

    public List<DownloadItem> Items { get; set; } = new();
}

public sealed class DownloadItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Description { get; set; }
}

/// <summary>GraphQL payload for authenticated clients.</summary>
public sealed class DownloadsConfig
{
    public List<DownloadItem> Items { get; init; } = new();
}
