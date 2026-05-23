namespace Tyresoles.Data.Features.DriveSync.Entities;

public sealed class DriveSyncPreparedUploadSession
{
    public string UploadUrl { get; init; } = string.Empty;
    public string ParentFolderId { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}
