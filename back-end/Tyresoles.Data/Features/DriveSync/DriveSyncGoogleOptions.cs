namespace Tyresoles.Data.Features.DriveSync;

/// <summary>Configuration for Drive sync using backend-managed OAuth admin credentials and API-proxied restore.</summary>
public sealed class DriveSyncGoogleOptions
{
    public const string SectionName = "DriveSync";

    /// <summary>When false, upload credential requests and restore endpoints respond with a clear configuration error.</summary>
    public bool Enabled { get; set; }

    /// <summary>Optional: restrict backup folders to these Shared Drive IDs (empty = only validate folder exists and is a directory).</summary>
    public string[] AllowedSharedDriveIds { get; set; } = [];

    /// <summary>
    /// Parent Drive folder id where per-user backup subfolders are created. Must be accessible to the OAuth admin user.
    /// </summary>
    public string UserBackupFoldersParentId { get; set; } = "";

    /// <summary>Cache duration for computed folder usage (recursive size sum).</summary>
    public int UsageCacheSeconds { get; set; } = 300;

    /// <summary>Hint only; Google issues ~3600s tokens. Client should refresh before expiry.</summary>
    public int UploadTokenLifetimeSeconds { get; set; } = 3300;

    /// <summary>OAuth web client id (required for <c>OAuthAdmin</c> mode).</summary>
    public string OAuthClientId { get; set; } = "";

    /// <summary>OAuth web client secret (required for <c>OAuthAdmin</c> mode).</summary>
    public string OAuthClientSecret { get; set; } = "";

    /// <summary>OAuth callback URL configured in Google Cloud Console.</summary>
    public string OAuthRedirectUri { get; set; } = "";
}
