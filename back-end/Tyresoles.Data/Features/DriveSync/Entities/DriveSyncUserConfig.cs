using System;

namespace Tyresoles.Data.Features.DriveSync.Entities;

/// <summary>
/// API shape for Drive sync policy. Persisted on Nav Live User table columns
/// <c>Backup G Drive Folder ID</c>, <c>Backup Storage Quota (GB)</c>, <c>Backup Allowed File Types</c>.
/// </summary>
public sealed class DriveSyncUserConfig
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Nav <c>User Name</c> (same as admin Users screen / JWT user id).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The Google Drive Folder ID specifically assigned to this user by the Admin.
    /// </summary>
    public string TargetFolderId { get; set; } = string.Empty;

    /// <summary>
    /// Quota limit in Bytes allocated to the user.
    /// </summary>
    public long QuotaBytes { get; set; }

    /// <summary>
    /// Currently recorded used bytes from the last successful sync check.
    /// </summary>
    public long UsedBytes { get; set; }

    /// <summary>
    /// JSON array of allowed extensions, e.g., ["pst", "docx", "pdf"]. Leave null or empty to allow all.
    /// </summary>
    public string? AllowedExtensionsJson { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
