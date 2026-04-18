using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.DriveSync.Entities;

namespace Tyresoles.Data.Features.DriveSync;

public sealed class DriveSyncService : IDriveSyncService
{
    private readonly DriveSyncDbContext _db;

    public DriveSyncService(DriveSyncDbContext db)
    {
        _db = db;
    }

    public async Task<DriveSyncUserConfig?> GetUserConfigAsync(string userId, CancellationToken ct = default)
    {
        return await _db.UserConfigs.FirstOrDefaultAsync(c => c.UserId == userId, ct);
    }

    public async Task<DriveSyncUserConfig> SaveUserConfigAsync(DriveSyncUserConfig input, string adminUserId, CancellationToken ct = default)
    {
        var existing = await _db.UserConfigs.FirstOrDefaultAsync(c => c.UserId == input.UserId, ct);
        if (existing != null)
        {
            existing.TargetFolderId = input.TargetFolderId;
            existing.QuotaBytes = input.QuotaBytes;
            existing.AllowedExtensionsJson = input.AllowedExtensionsJson;
            existing.IsActive = input.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        input.Id = Guid.NewGuid();
        input.CreatedAt = DateTime.UtcNow;
        input.UpdatedAt = DateTime.UtcNow;
        _db.UserConfigs.Add(input);
        await _db.SaveChangesAsync(ct);
        return input;
    }

    public async Task<string> RequestJitSyncTokenAsync(string userId, long requestedUploadBytes, CancellationToken ct = default)
    {
        var config = await GetUserConfigAsync(userId, ct);
        if (config == null || !config.IsActive)
            throw new InvalidOperationException("User DriveSync configuration is not found or inactive.");

        if (config.QuotaBytes > 0 && (config.UsedBytes + requestedUploadBytes) > config.QuotaBytes)
            throw new InvalidOperationException("Upload exceeds allocated Google Drive quota limit.");

        // TODO: Integrate actual Google.Apis.Auth here using the backend's Service Account 
        // to generate a scoped access token for `config.TargetFolderId`.
        // For now, we return a mock token structure that Tauri Rclone setup will expect.
        await Task.Delay(100, ct); 
        return $"mock_jit_token_for_{userId}_" + Guid.NewGuid().ToString("N");
    }
}
