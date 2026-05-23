namespace Tyresoles.Data.Features.DriveSync.Entities;

/// <summary>Short-lived OAuth access token for the desktop client to upload directly to Drive (hybrid path).</summary>
public sealed class DriveSyncUploadCredentials
{
    public string AccessToken { get; set; } = "";

    /// <summary>UTC expiry; client should request a new token before this time.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    public string FolderId { get; set; } = "";
}
