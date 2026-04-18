using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Data.Features.DriveSync.Entities;

namespace Tyresoles.Data.Features.DriveSync;

public interface IDriveSyncService
{
    Task<DriveSyncUserConfig?> GetUserConfigAsync(string userId, CancellationToken ct = default);
    Task<DriveSyncUserConfig> SaveUserConfigAsync(DriveSyncUserConfig input, string adminUserId, CancellationToken ct = default);
    
    /// <summary>
    /// Generates a short-lived JIT Access token using the backend's Google Service Account,
    /// so the Tauri app can upload directly to the mapped TargetFolderId securely.
    /// It enforces quota limits before issuing the token.
    /// </summary>
    Task<string> RequestJitSyncTokenAsync(string userId, long requestedUploadBytes, CancellationToken ct = default);
}
