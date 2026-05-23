using System.IO;
using System.Linq;
using System.Net;
using System.Collections.Concurrent;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.DriveSync;
using Tyresoles.Data.Features.DriveSync.Entities;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Tyresoles.Web.Features.DriveSync;

/// <summary>Google Drive v3 calls under the admin service account (Shared Drive compatible).</summary>
public sealed class GoogleDriveBackupGateway : IGoogleDriveBackupGateway
{
    private const string CacheKeyPrefix = "Tyresoles:DriveSync:Usage:";
    private readonly DriveSyncGoogleOptions _options;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GoogleDriveBackupGateway> _log;
    private readonly IWebHostEnvironment _env;
    private readonly IDriveSyncOAuthService _oauth;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathSegmentLocks = new(StringComparer.Ordinal);

    public GoogleDriveBackupGateway(
        IOptions<DriveSyncGoogleOptions> options,
        IDistributedCache cache,
        ILogger<GoogleDriveBackupGateway> log,
        IWebHostEnvironment env,
        IDriveSyncOAuthService oauth)
    {
        _options = options.Value;
        _cache = cache;
        _log = log;
        _env = env;
        _oauth = oauth;
    }

    public async Task ValidateUserBackupFolderAsync(string folderId, CancellationToken ct = default)
    {
        EnsureConfigured();
        folderId = (folderId ?? "").Trim();
        if (string.IsNullOrEmpty(folderId))
            throw new InvalidOperationException("Backup folder id is empty.");

        var svc = CreateDriveService();
        var get = svc.Files.Get(folderId);
        get.SupportsAllDrives = true;
        get.Fields = "id,mimeType,driveId,trashed";
        DriveFile meta;
        try
        {
            meta = await get.ExecuteAsync(ct).ConfigureAwait(false);
        }
        catch (Google.GoogleApiException ex) { 
            _log.LogWarning(ex, "Drive files.get failed for folder id length {Len}: {Status}", folderId.Length, ex.HttpStatusCode);

            if (ex.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(
                    $"Cannot access Google Drive folder (check DriveSync:UserBackupFoldersParentId or the user backup folder id). " +
                    $"HTTP {(int)ex.HttpStatusCode}. Share that folder with your OAuth Admin user as Editor / Content manager. " +
                    "Google often returns 404 when the admin user has no access.", ex);
            }

            throw new InvalidOperationException(
                $"Google Drive API error validating folder: {(int)ex.HttpStatusCode} {ex.Message}", ex);
        }

        if (meta.Trashed == true)
            throw new InvalidOperationException("Backup folder is in trash.");

        if (!string.Equals(meta.MimeType, "application/vnd.google-apps.folder", StringComparison.Ordinal))
            throw new InvalidOperationException("Backup G Drive Folder ID must reference a folder, not a file.");

        // Service accounts can upload to personal My Drive folders shared with them as Editor.
        // This utilizes the folder owner's quota, so we no longer strictly enforce Shared Drives.
        // if (string.IsNullOrWhiteSpace(meta.DriveId)) { ... }

        if (_options.AllowedSharedDriveIds is { Length: > 0 })
        {
            var driveId = meta.DriveId ?? "";
            if (string.IsNullOrEmpty(driveId) || Array.IndexOf(_options.AllowedSharedDriveIds, driveId) < 0)
            {
                throw new InvalidOperationException(
                    "Backup folder is not on an allowed Shared Drive. Update DriveSync:AllowedSharedDriveIds or move the folder.");
            }
        }
    }

    public async Task<string> CreateChildBackupFolderAsync(string parentFolderId, string folderName, CancellationToken ct = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(parentFolderId))
            throw new ArgumentException("Parent folder id is required.", nameof(parentFolderId));
        if (string.IsNullOrWhiteSpace(folderName))
            throw new ArgumentException("Folder name is required.", nameof(folderName));

        await ValidateUserBackupFolderAsync(parentFolderId, ct).ConfigureAwait(false);

        var svc = CreateDriveService();
        var meta = new DriveFile
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new List<string> { parentFolderId }
        };
        var req = svc.Files.Create(meta);
        req.SupportsAllDrives = true;
        req.Fields = "id";
        var created = await req.ExecuteAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(created.Id))
            throw new InvalidOperationException("Google Drive did not return a new folder id.");

        _log.LogInformation("Created backup subfolder {FolderName} ({FolderId}) under parent {ParentId}", folderName, created.Id, parentFolderId);
        return created.Id;
    }

    public async Task<long> GetFolderTreeUsageBytesAsync(string folderId, CancellationToken ct = default)
    {
        EnsureConfigured();
        var cacheKey = CacheKeyPrefix + folderId;
        var cached = await _cache.GetStringAsync(cacheKey, ct).ConfigureAwait(false);
        if (cached is not null && long.TryParse(cached, out var parsed))
            return parsed;

        var svc = CreateDriveService();
        var total = await SumTreeBytesAsync(svc, folderId, ct).ConfigureAwait(false);
        var ttl = TimeSpan.FromSeconds(Math.Clamp(_options.UsageCacheSeconds, 30, 3600));
        await _cache.SetStringAsync(cacheKey, total.ToString(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct)
            .ConfigureAwait(false);
        return total;
    }

    public async Task<(string Token, DateTime ExpiresAtUtc)> GetUploadAccessTokenAsync(CancellationToken ct = default)
    {
        EnsureConfigured();
        var utc = DateTime.UtcNow;
        var token = await _oauth.GetValidAccessTokenAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("Google did not return a valid OAuth access token for the admin user.");

        var skew = TimeSpan.FromSeconds(Math.Clamp(_options.UploadTokenLifetimeSeconds, 300, 3600));
        return (token, utc.Add(skew));
    }

    public async Task<DriveSyncPreparedUploadSession> StartResumableUploadAsync(
        string rootFolderId,
        string relativePath,
        string fileName,
        string mimeType,
        CancellationToken ct = default)
    {
        EnsureConfigured();
        await ValidateUserBackupFolderAsync(rootFolderId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("File name is required.");

        var svc = CreateDriveService();
        var normalized = NormalizeRelativePath(relativePath);
        var parentFolderId = await ResolveOrCreatePathAsync(svc, rootFolderId, normalized, ct).ConfigureAwait(false);
        await DeleteExistingFileIfPresentAsync(svc, parentFolderId, fileName, ct).ConfigureAwait(false);

        var metadata = new DriveFile
        {
            Name = fileName,
            Parents = new[] { parentFolderId }
        };

        await using var emptyStream = new MemoryStream(Array.Empty<byte>());
        var uploadRequest = svc.Files.Create(metadata, emptyStream, string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType);
        uploadRequest.Fields = "id,name";
        uploadRequest.SupportsAllDrives = true;

        var uploadUri = await uploadRequest.InitiateSessionAsync(ct).ConfigureAwait(false);
        return new DriveSyncPreparedUploadSession
        {
            UploadUrl = uploadUri.ToString(),
            ParentFolderId = parentFolderId,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };
    }

    public async Task<IReadOnlyList<DriveSyncBackupFileInfo>> ListBackupFilesAsync(string rootFolderId, CancellationToken ct = default)
    {
        EnsureConfigured();
        var svc = CreateDriveService();
        const int maxFiles = 2000;
        var list = new List<DriveSyncBackupFileInfo>();
        var folders = new Queue<string>();
        folders.Enqueue(rootFolderId);

        while (folders.Count > 0 && list.Count < maxFiles)
        {
            ct.ThrowIfCancellationRequested();
            var folderId = folders.Dequeue();
            string? pageToken = null;
            do
            {
                var req = svc.Files.List();
                req.Q = $"'{folderId}' in parents and trashed = false";
                req.Fields = "nextPageToken, files(id, name, size, mimeType, modifiedTime)";
                req.PageSize = 200;
                req.SupportsAllDrives = true;
                req.IncludeItemsFromAllDrives = true;
                req.PageToken = pageToken;

                var result = await req.ExecuteAsync(ct).ConfigureAwait(false);
                if (result.Files is null) break;

                foreach (var f in result.Files)
                {
                    if (list.Count >= maxFiles)
                        break;
                    if (IsFolder(f))
                        folders.Enqueue(f.Id);
                    else
                    {
                        list.Add(new DriveSyncBackupFileInfo
                        {
                            Id = f.Id,
                            Name = f.Name ?? "",
                            Size = f.Size,
                            MimeType = f.MimeType,
                            ModifiedTimeUtc = ParseModifiedUtc(f)
                        });
                    }
                }

                pageToken = result.NextPageToken;
            } while (pageToken is not null && list.Count < maxFiles);
        }

        if (list.Count >= maxFiles)
            _log.LogWarning("Drive sync list truncated at {Max} files for folder {FolderId}", maxFiles, rootFolderId);

        return list;
    }

    public async Task<bool> IsFileInUserBackupTreeAsync(string fileId, string userRootFolderId, CancellationToken ct = default)
    {
        if (string.Equals(fileId, userRootFolderId, StringComparison.Ordinal))
            return true;

        EnsureConfigured();
        var svc = CreateDriveService();
        var current = fileId;
        for (var depth = 0; depth < 64; depth++)
        {
            var get = svc.Files.Get(current);
            get.SupportsAllDrives = true;
            get.Fields = "id,parents";
            DriveFile meta;
            try
            {
                meta = await get.ExecuteAsync(ct).ConfigureAwait(false);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            if (meta.Parents is null || meta.Parents.Count == 0)
                return false;

            foreach (var p in meta.Parents)
            {
                if (string.Equals(p, userRootFolderId, StringComparison.Ordinal))
                    return true;
            }

            current = meta.Parents[0];
        }

        return false;
    }

    public async Task<(string FileName, string MimeType)?> GetBackupFileForDownloadMetadataAsync(string fileId, CancellationToken ct = default)
    {
        EnsureConfigured();
        var svc = CreateDriveService();

        var getMeta = svc.Files.Get(fileId);
        getMeta.SupportsAllDrives = true;
        getMeta.Fields = "id,name,mimeType,trashed";
        DriveFile meta;
        try
        {
            meta = await getMeta.ExecuteAsync(ct).ConfigureAwait(false);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (meta.Trashed == true || IsFolder(meta))
            return null;

        var name = meta.Name ?? "download";
        var mime = string.IsNullOrEmpty(meta.MimeType) ? "application/octet-stream" : meta.MimeType;
        return (name, mime);
    }

    public async Task StreamBackupFileToAsync(string fileId, Stream destination, CancellationToken ct = default)
    {
        EnsureConfigured();
        var svc = CreateDriveService();
        var getMedia = svc.Files.Get(fileId);
        getMedia.SupportsAllDrives = true;
        await getMedia.DownloadAsync(destination, ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteFileByPathAsync(string rootFolderId, string relativePath, string fileName, CancellationToken ct = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("fileName is required.");
        await ValidateUserBackupFolderAsync(rootFolderId, ct).ConfigureAwait(false);
        var svc = CreateDriveService();
        var normalized = NormalizeRelativePath(relativePath);
        var parent = await ResolveExistingPathAsync(svc, rootFolderId, normalized, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(parent))
            return false;
        var req = svc.Files.List();
        req.Q = $"'{parent}' in parents and trashed = false and mimeType != 'application/vnd.google-apps.folder' and name = '{EscapeQueryLiteral(fileName)}'";
        req.Fields = "files(id,name)";
        req.PageSize = 1;
        req.SupportsAllDrives = true;
        req.IncludeItemsFromAllDrives = true;
        var list = await req.ExecuteAsync(ct).ConfigureAwait(false);
        var existing = list.Files?.FirstOrDefault();
        if (existing?.Id is null)
            return false;
        var del = svc.Files.Delete(existing.Id);
        del.SupportsAllDrives = true;
        await del.ExecuteAsync(ct).ConfigureAwait(false);
        return true;
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("DriveSync is disabled in configuration.");

        if (string.IsNullOrWhiteSpace(_options.OAuthClientId)
            || string.IsNullOrWhiteSpace(_options.OAuthClientSecret)
            || string.IsNullOrWhiteSpace(_options.OAuthRedirectUri))
        {
            throw new InvalidOperationException("DriveSync OAuth admin mode requires OAuthClientId, OAuthClientSecret, and OAuthRedirectUri.");
        }
    }

    private GoogleCredential CreateCredential()
    {
        var token = _oauth.GetValidAccessTokenAsync().GetAwaiter().GetResult();
        return GoogleCredential.FromAccessToken(token);
    }

    private DriveService CreateDriveService()
    {
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = CreateCredential(),
            ApplicationName = "Tyresoles Backup"
        });
    }

    private static bool IsFolder(DriveFile f) =>
        string.Equals(f.MimeType, "application/vnd.google-apps.folder", StringComparison.Ordinal);

    private static DateTime? ParseModifiedUtc(DriveFile f) =>
        f.ModifiedTimeDateTimeOffset?.UtcDateTime;

    private static async Task<long> SumTreeBytesAsync(DriveService svc, string rootFolderId, CancellationToken ct)
    {
        long sum = 0;
        var folders = new Queue<string>();
        folders.Enqueue(rootFolderId);

        while (folders.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var folderId = folders.Dequeue();
            string? pageToken = null;
            do
            {
                var req = svc.Files.List();
                req.Q = $"'{folderId}' in parents and trashed = false";
                req.Fields = "nextPageToken, files(id, size, mimeType)";
                req.PageSize = 1000;
                req.SupportsAllDrives = true;
                req.IncludeItemsFromAllDrives = true;
                req.PageToken = pageToken;

                var result = await req.ExecuteAsync(ct).ConfigureAwait(false);
                if (result.Files is null) break;

                foreach (var f in result.Files)
                {
                    if (IsFolder(f))
                        folders.Enqueue(f.Id);
                    else if (f.Size is { } sz)
                        sum += sz;
                }

                pageToken = result.NextPageToken;
            } while (pageToken is not null);
        }

        return sum;
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return string.Empty;

        var path = relativePath.Trim().Replace('\\', '/');
        while (path.StartsWith("/", StringComparison.Ordinal))
            path = path[1..];
        if (string.Equals(path, ".", StringComparison.Ordinal))
            return string.Empty;
        return path;
    }

    private static async Task<string> ResolveOrCreatePathAsync(DriveService svc, string rootFolderId, string relativePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return rootFolderId;

        var segments = relativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(seg => !string.Equals(seg, ".", StringComparison.Ordinal) && !string.Equals(seg, "..", StringComparison.Ordinal))
            .ToArray();

        var current = rootFolderId;
        foreach (var segment in segments)
        {
            var lockKey = $"{current}|{segment}";
            var segmentLock = PathSegmentLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await segmentLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
              var child = await FindChildFolderAsync(svc, current, segment, ct).ConfigureAwait(false);
              if (string.IsNullOrWhiteSpace(child))
              {
                var createReq = svc.Files.Create(new DriveFile
                {
                    Name = segment,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new[] { current }
                });
                createReq.Fields = "id";
                createReq.SupportsAllDrives = true;
                var created = await createReq.ExecuteAsync(ct).ConfigureAwait(false);
                child = created.Id;
              }

              current = child!;
            }
            finally
            {
                segmentLock.Release();
            }
        }

        return current;
    }

    private static async Task<string?> ResolveExistingPathAsync(DriveService svc, string rootFolderId, string relativePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return rootFolderId;
        var segments = relativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(seg => !string.Equals(seg, ".", StringComparison.Ordinal) && !string.Equals(seg, "..", StringComparison.Ordinal))
            .ToArray();
        var current = rootFolderId;
        foreach (var segment in segments)
        {
            var child = await FindChildFolderAsync(svc, current, segment, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(child))
                return null;
            current = child!;
        }
        return current;
    }

    private static async Task<string?> FindChildFolderAsync(DriveService svc, string parentFolderId, string folderName, CancellationToken ct)
    {
        var req = svc.Files.List();
        req.Q = $"'{parentFolderId}' in parents and trashed = false and mimeType = 'application/vnd.google-apps.folder' and name = '{EscapeQueryLiteral(folderName)}'";
        req.Fields = "files(id,name)";
        req.PageSize = 1;
        req.SupportsAllDrives = true;
        req.IncludeItemsFromAllDrives = true;
        var result = await req.ExecuteAsync(ct).ConfigureAwait(false);
        return result.Files?.FirstOrDefault()?.Id;
    }

    private static async Task DeleteExistingFileIfPresentAsync(DriveService svc, string parentFolderId, string fileName, CancellationToken ct)
    {
        var req = svc.Files.List();
        req.Q = $"'{parentFolderId}' in parents and trashed = false and mimeType != 'application/vnd.google-apps.folder' and name = '{EscapeQueryLiteral(fileName)}'";
        req.Fields = "files(id,name)";
        req.PageSize = 1;
        req.SupportsAllDrives = true;
        req.IncludeItemsFromAllDrives = true;
        var list = await req.ExecuteAsync(ct).ConfigureAwait(false);
        var existing = list.Files?.FirstOrDefault();
        if (existing?.Id is null)
            return;

        var del = svc.Files.Delete(existing.Id);
        del.SupportsAllDrives = true;
        await del.ExecuteAsync(ct).ConfigureAwait(false);
    }

    private static string EscapeQueryLiteral(string value) =>
        (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
}
