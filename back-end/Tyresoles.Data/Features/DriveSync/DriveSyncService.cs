using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
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
    private readonly IGoogleDriveBackupGateway _drive;
    private readonly DriveSyncGoogleOptions _options;

    public DriveSyncService(
        IUserService users,
        IGoogleDriveBackupGateway drive,
        IOptions<DriveSyncGoogleOptions> options)
    {
        _users = users;
        _drive = drive;
        _options = options.Value;
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

    public async Task<DriveSyncUploadCredentials> RequestUploadCredentialsAsync(string userId, long requestedUploadBytes, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Drive sync is disabled. Set DriveSync:Enabled to true and configure the service account.");

        if (requestedUploadBytes < 0)
            throw new InvalidOperationException("requestedUploadBytes must be non-negative.");

        var config = await GetUserConfigAsync(userId, ct).ConfigureAwait(false);
        if (config == null || !config.IsActive)
            throw new InvalidOperationException("User Drive sync is not enabled. Set a backup folder ID on the user record.");

        await _drive.ValidateUserBackupFolderAsync(config.TargetFolderId, ct).ConfigureAwait(false);

        var used = await _drive.GetFolderTreeUsageBytesAsync(config.TargetFolderId, ct).ConfigureAwait(false);
        if (config.QuotaBytes > 0 && used + requestedUploadBytes > config.QuotaBytes)
            throw new InvalidOperationException("Upload would exceed the allocated backup quota.");

        var (token, exp) = await _drive.GetUploadAccessTokenAsync(ct).ConfigureAwait(false);
        return new DriveSyncUploadCredentials
        {
            AccessToken = token,
            ExpiresAtUtc = exp,
            FolderId = config.TargetFolderId
        };
    }

    public async Task<DriveSyncPreparedUploadSession> PrepareUploadSessionAsync(
        string userId,
        string relativePath,
        string fileName,
        long fileSizeBytes,
        string? mimeType,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Drive sync is disabled. Set DriveSync:Enabled to true and configure the service account.");
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("userId is required.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("fileName is required.");
        if (fileSizeBytes < 0)
            throw new InvalidOperationException("fileSizeBytes must be non-negative.");

        var config = await GetUserConfigAsync(userId, ct).ConfigureAwait(false);
        if (config == null || !config.IsActive)
            throw new InvalidOperationException("User Drive sync is not enabled. Set a backup folder ID on the user record.");

        ValidateAllowedExtension(config.AllowedExtensionsJson, fileName);
        await _drive.ValidateUserBackupFolderAsync(config.TargetFolderId, ct).ConfigureAwait(false);

        var used = await _drive.GetFolderTreeUsageBytesAsync(config.TargetFolderId, ct).ConfigureAwait(false);
        if (config.QuotaBytes > 0 && used + fileSizeBytes > config.QuotaBytes)
            throw new InvalidOperationException("Upload would exceed the allocated backup quota.");

        var safeMime = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType.Trim();
        return await _drive.StartResumableUploadAsync(
                config.TargetFolderId,
                relativePath ?? string.Empty,
                fileName.Trim(),
                safeMime,
                ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DriveSyncBackupFileInfo>> GetBackupFilesForRestoreAsync(string userId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Drive sync is disabled.");

        var config = await GetUserConfigAsync(userId, ct).ConfigureAwait(false);
        if (config == null || !config.IsActive)
            throw new InvalidOperationException("User Drive sync is not enabled.");

        await _drive.ValidateUserBackupFolderAsync(config.TargetFolderId, ct).ConfigureAwait(false);
        return await _drive.ListBackupFilesAsync(config.TargetFolderId, ct).ConfigureAwait(false);
    }

    public async Task<DriveSyncUserConfig> ProvisionAndAssignBackupFolderAsync(
        string targetUserId,
        string? folderDisplayName,
        bool replaceExisting,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
            throw new InvalidOperationException("targetUserId is required.");

        if (!_options.Enabled)
            throw new InvalidOperationException("Drive sync is disabled. Set DriveSync:Enabled to true.");

        if (string.IsNullOrWhiteSpace(_options.UserBackupFoldersParentId))
            throw new InvalidOperationException("DriveSync:UserBackupFoldersParentId is not configured (parent folder for new user backups).");

        var existing = await _users.GetUserAsync(targetUserId, ct).ConfigureAwait(false);
        if (existing == null)
            throw new InvalidOperationException("User not found.");

        if (!string.IsNullOrWhiteSpace(existing.BackupGDriveFolderID) && !replaceExisting)
        {
            throw new InvalidOperationException(
                "This user already has a backup folder ID. Pass replaceExisting: true to create another folder and update the user record (the old Drive folder is not deleted).");
        }

        await _drive.ValidateUserBackupFolderAsync(_options.UserBackupFoldersParentId, ct).ConfigureAwait(false);

        var safeName = SanitizeBackupFolderDisplayName(folderDisplayName, existing.UserId);
        var newFolderId = await _drive
            .CreateChildBackupFolderAsync(_options.UserBackupFoldersParentId, safeName, ct)
            .ConfigureAwait(false);

        var ok = await _users.SetProfileAsync(
                targetUserId,
                new ProfileUpdateInput { BackupGDriveFolderID = newFolderId },
                ct)
            .ConfigureAwait(false);
        if (!ok)
            throw new InvalidOperationException("Failed to save the new backup folder ID to the user profile.");

        return await GetUserConfigAsync(targetUserId, ct).ConfigureAwait(false)
               ?? throw new InvalidOperationException("User not found after provisioning.");
    }

    /// <summary>
    /// Drive subfolder name: Nav <c>UserName</c> with <c>TYRESOLES\</c> stripped and whole-word <c>Backup</c> removed (case-insensitive).
    /// Optional <paramref name="displayName"/> overrides the source string before the same normalization.
    /// </summary>
    private static string SanitizeBackupFolderDisplayName(string? displayName, string navUserName)
    {
        var source = string.IsNullOrWhiteSpace(displayName) ? navUserName : displayName.Trim();
        var name = NormalizeNavUserNameForGDriveFolder(source);
        if (string.IsNullOrWhiteSpace(name))
            name = FallbackGDriveFolderStem(navUserName);

        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Replace('\t', '_').Replace('\n', '_').Replace('\r', '_');
        if (name.Length > 200)
            name = name[..200];
        if (string.IsNullOrWhiteSpace(name))
            name = FallbackGDriveFolderStem(navUserName);
        return name;
    }

    /// <summary>Strip domain prefix <c>TYRESOLES\</c> and remove whole-word <c>Backup</c> (any casing).</summary>
    private static string NormalizeNavUserNameForGDriveFolder(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        const string domainPrefix = "TYRESOLES\\";
        if (s.Length >= domainPrefix.Length && s.StartsWith(domainPrefix, StringComparison.OrdinalIgnoreCase))
            s = s[domainPrefix.Length..].Trim();

        string prev;
        do
        {
            prev = s;
            s = Regex.Replace(s, @"\bBackup\b", " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\s+", " ").Trim();
        } while (s != prev);

        return s;
    }

    private static string FallbackGDriveFolderStem(string navUserName)
    {
        var s = (navUserName ?? "").Trim();
        const string domainPrefix = "TYRESOLES\\";
        if (s.Length >= domainPrefix.Length && s.StartsWith(domainPrefix, StringComparison.OrdinalIgnoreCase))
            s = s[domainPrefix.Length..].Trim();

        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                sb.Append(c);
        }

        var t = sb.ToString().Trim('_', '-');
        t = NormalizeNavUserNameForGDriveFolder(t);
        if (string.IsNullOrEmpty(t))
        {
            var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(navUserName ?? "")));
            t = "User_" + hash[..Math.Min(8, hash.Length)];
        }

        return t;
    }

    private static DriveSyncUserConfig MapFromUserDetail(UserDetail u)
    {
        var quotaBytes = QuotaGbToBytes(u.BackupStorageQuotaGB);
        var folder = u.BackupGDriveFolderID ?? string.Empty;
        var isActive = !string.IsNullOrWhiteSpace(folder);

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

    private static void ValidateAllowedExtension(string? allowedExtensionsJson, string fileName)
    {
        var allowed = ParseAllowedExtensions(allowedExtensionsJson);
        if (allowed is null || allowed.Count == 0)
            return;

        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
            throw new InvalidOperationException($"File type '.{ext}' is not allowed for this user.");
    }

    private static HashSet<string>? ParseAllowedExtensions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (s.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(s);
                if (arr == null)
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return new HashSet<string>(
                    arr.Select(v => (v ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant()).Where(v => v.Length > 0),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        return new HashSet<string>(
            s.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim().TrimStart('.').ToLowerInvariant())
                .Where(v => v.Length > 0),
            StringComparer.OrdinalIgnoreCase);
    }
}
