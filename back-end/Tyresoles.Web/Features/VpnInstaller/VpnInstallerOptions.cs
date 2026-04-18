namespace Tyresoles.Web.Features.VpnInstaller;

/// <summary>Configuration for the desktop VPN installer download (served URL + optional integrity and zip layout).</summary>
public sealed class VpnInstallerOptions
{
    public const string SectionName = "VpnInstaller";

    /// <summary>HTTPS URL to the VPN installer binary or zip archive (CDN / static files).</summary>
    public string DownloadUrl { get; set; } = "";

    /// <summary>Optional SHA-256 of the final installer file (hex), verified by the desktop client after download / extraction.</summary>
    public string? Sha256Hex { get; set; }

    /// <summary>Local file name to store under the app data folder (e.g. FortiClientVPNSetup_x64.exe).</summary>
    public string? FileName { get; set; }

    /// <summary>When true, <see cref="DownloadUrl"/> points to a zip; the client extracts the installer after download.</summary>
    public bool IsZipArchive { get; set; }

    /// <summary>Optional path inside the zip (forward slashes). If empty, the client picks the first .exe/.msi in the archive root.</summary>
    public string? ZipEntryName { get; set; }
}

/// <summary>GraphQL payload for authenticated clients (desktop app).</summary>
public sealed class VpnInstallerConfig
{
    public string DownloadUrl { get; init; } = "";
    public string? Sha256Hex { get; init; }
    public string? FileName { get; init; }
    public bool IsZipArchive { get; init; }
    public string? ZipEntryName { get; init; }
}
