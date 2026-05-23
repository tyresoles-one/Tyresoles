using Tyresoles.Data.Features.DriveSync.Entities;

namespace Tyresoles.Data.Features.DriveSync;

/// <summary>Server-side Google Drive operations using the admin service account.</summary>
public interface IGoogleDriveBackupGateway
{
    /// <summary>Ensure the folder exists, is a directory, and optionally belongs to an allowed Shared Drive.</summary>
    Task ValidateUserBackupFolderAsync(string folderId, CancellationToken ct = default);

    /// <summary>Create a subfolder under <paramref name="parentFolderId"/> and return the new folder id.</summary>
    Task<string> CreateChildBackupFolderAsync(string parentFolderId, string folderName, CancellationToken ct = default);

    /// <summary>Total logical size (bytes) of all non-trashed files under the folder tree.</summary>
    Task<long> GetFolderTreeUsageBytesAsync(string folderId, CancellationToken ct = default);

    /// <summary>OAuth access token for the service account with Drive scope (for client direct upload).</summary>
    Task<(string Token, DateTime ExpiresAtUtc)> GetUploadAccessTokenAsync(CancellationToken ct = default);
    Task<DriveSyncPreparedUploadSession> StartResumableUploadAsync(
        string rootFolderId,
        string relativePath,
        string fileName,
        string mimeType,
        CancellationToken ct = default);

    Task<IReadOnlyList<DriveSyncBackupFileInfo>> ListBackupFilesAsync(string rootFolderId, CancellationToken ct = default);

    /// <summary>True if <paramref name="fileId"/> is the folder or any descendant within <paramref name="userRootFolderId"/>.</summary>
    Task<bool> IsFileInUserBackupTreeAsync(string fileId, string userRootFolderId, CancellationToken ct = default);

    /// <summary>Null if missing, trashed, or a Drive folder (not a downloadable file).</summary>
    Task<(string FileName, string MimeType)?> GetBackupFileForDownloadMetadataAsync(string fileId, CancellationToken ct = default);

    Task StreamBackupFileToAsync(string fileId, Stream destination, CancellationToken ct = default);

    /// <summary>Delete a non-folder file by relative path and file name under the user's root backup folder.</summary>
    Task<bool> DeleteFileByPathAsync(string rootFolderId, string relativePath, string fileName, CancellationToken ct = default);
}
