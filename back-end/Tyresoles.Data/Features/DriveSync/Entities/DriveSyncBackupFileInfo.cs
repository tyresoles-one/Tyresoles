namespace Tyresoles.Data.Features.DriveSync.Entities;

/// <summary>One file in the user backup tree (for restore listing via GraphQL).</summary>
public sealed class DriveSyncBackupFileInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long? Size { get; set; }
    public string? MimeType { get; set; }
    public DateTime? ModifiedTimeUtc { get; set; }
}
