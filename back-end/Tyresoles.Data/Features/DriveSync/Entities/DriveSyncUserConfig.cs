using System;

namespace Tyresoles.Data.Features.DriveSync.Entities;

public sealed class DriveSyncUserConfig
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// User ID mapping to the central user table.
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
