using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Data.Features.DriveSync.Entities;

namespace Tyresoles.Data.Features.DriveSync;

/// <summary>
/// Reads and updates Drive sync policy on Nav Live <c>User</c> (folder id, quota GB, file types).
/// Prefer admin <see cref="Tyresoles.Data.Features.Admin.User.IUserService.SetProfileAsync"/> / Users UI for routine edits.
/// </summary>
public interface IDriveSyncService
{
    Task<DriveSyncUserConfig?> GetUserConfigAsync(string userId, CancellationToken ct = default);
    Task<DriveSyncUserConfig> SaveUserConfigAsync(DriveSyncUserConfig input, string adminUserId, CancellationToken ct = default);
    
    /// <summary>
    /// Short-lived OAuth access token (service account) so the desktop client can upload directly to <see cref="DriveSyncUserConfig.TargetFolderId"/>.
    /// Quota is enforced using cached folder tree usage.
    /// </summary>
    Task<DriveSyncUploadCredentials> RequestUploadCredentialsAsync(string userId, long requestedUploadBytes, CancellationToken ct = default);
    Task<DriveSyncPreparedUploadSession> PrepareUploadSessionAsync(
        string userId,
        string relativePath,
        string fileName,
        long fileSizeBytes,
        string? mimeType,
        CancellationToken ct = default);

    Task<IReadOnlyList<DriveSyncBackupFileInfo>> GetBackupFilesForRestoreAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Admin: create a Google Drive folder under configured <see cref="DriveSyncGoogleOptions.UserBackupFoldersParentId"/> and set <c>BackupGDriveFolderID</c> on the Nav user.
    /// </summary>
    Task<DriveSyncUserConfig> ProvisionAndAssignBackupFolderAsync(
        string targetUserId,
        string? folderDisplayName,
        bool replaceExisting,
        CancellationToken ct = default);
}
