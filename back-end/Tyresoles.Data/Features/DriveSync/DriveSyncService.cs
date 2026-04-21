using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Data.Features.Admin.User;
using Tyresoles.Data.Features.DriveSync.Entities;

namespace Tyresoles.Data.Features.DriveSync;

/// <summary>
/// Drive sync policy is stored on Nav Live <c>User</c>: Backup G Drive Folder ID, quota (GB), allowed file types.
/// Admin edits these fields on the Users admin screen (<c>updateUserDetails</c> / <c>SetProfileAsync</c>).
/// </summary>
public sealed class DriveSyncService : IDriveSyncService
{
    private readonly IUserService _users;

    public DriveSyncService(IUserService users)
    {
        _users = users;
    }

    public async Task<DriveSyncUserConfig?> GetUserConfigAsync(string userId, CancellationToken ct = default)
    {
        var u = await _users.GetUserAsync(userId, ct).ConfigureAwait(false);
        return u == null ? null : MapFromUserDetail(u);
    }

    public async Task<DriveSyncUserConfig> SaveUserConfigAsync(DriveSyncUserConfig input, string adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.UserId))
            throw new InvalidOperationException("UserId is required.");

        var folder = input.IsActive ? (input.TargetFolderId ?? string.Empty) : string.Empty;
        var quotaGb = input.IsActive ? BytesToQuotaGb(input.QuotaBytes) : 0m;
        var types = input.AllowedExtensionsJson ?? string.Empty;

        var ok = await _users.SetProfileAsync(
            input.UserId,
            new ProfileUpdateInput
            {
                BackupGDriveFolderID = folder,
                BackupStorageQuotaGB = quotaGb,
                BackupAllowedFileTypes = types
            },
            ct).ConfigureAwait(false);

        if (!ok)
            throw new InvalidOperationException("User not found; Drive sync settings were not saved.");

        var saved = await GetUserConfigAsync(input.UserId, ct).ConfigureAwait(false);
        return saved ?? throw new InvalidOperationException("User not found after save.");
    }

    public async Task<string> RequestJitSyncTokenAsync(string userId, long requestedUploadBytes, CancellationToken ct = default)
    {
        var config = await GetUserConfigAsync(userId, ct).ConfigureAwait(false);
        if (config == null || !config.IsActive)
            throw new InvalidOperationException("User DriveSync configuration is not found or inactive.");

        if (config.QuotaBytes > 0 && (config.UsedBytes + requestedUploadBytes) > config.QuotaBytes)
            throw new InvalidOperationException("Upload exceeds allocated Google Drive quota limit.");

        // TODO: Integrate Google.Apis.Auth (service account) scoped to config.TargetFolderId.
        await Task.Delay(100, ct).ConfigureAwait(false);
        return $"mock_jit_token_for_{userId}_" + Guid.NewGuid().ToString("N");
    }

    private static DriveSyncUserConfig MapFromUserDetail(UserDetail u)
    {
        var quotaBytes = QuotaGbToBytes(u.BackupStorageQuotaGB);
        var folder = u.BackupGDriveFolderID ?? string.Empty;
        var isActive = !string.IsNullOrWhiteSpace(folder) && u.BackupStorageQuotaGB > 0;

        return new DriveSyncUserConfig
        {
            Id = StableConfigId(u.UserId),
            UserId = u.UserId,
            TargetFolderId = folder,
            QuotaBytes = quotaBytes,
            UsedBytes = 0,
            AllowedExtensionsJson = string.IsNullOrEmpty(u.BackupAllowedFileTypes) ? null : u.BackupAllowedFileTypes,
            IsActive = isActive,
            CreatedAt = default,
            UpdatedAt = default
        };
    }

    /// <summary>Deterministic id for GraphQL clients; Nav User has no separate DriveSync row.</summary>
    private static Guid StableConfigId(string userId)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("Tyresoles:DriveSync:" + userId));
        return new Guid(hash);
    }

    private static long QuotaGbToBytes(decimal gb)
    {
        if (gb <= 0) return 0;
        var bytes = gb * 1024m * 1024m * 1024m;
        if (bytes >= long.MaxValue) return long.MaxValue;
        return (long)bytes;
    }

    private static decimal BytesToQuotaGb(long bytes)
    {
        if (bytes <= 0) return 0;
        return Math.Round(bytes / (1024m * 1024m * 1024m), 6, MidpointRounding.AwayFromZero);
    }
}
