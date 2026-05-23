using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.DriveSync;
using Tyresoles.Web;

namespace Tyresoles.Web.Features.DriveSync;

public static class DriveSyncEndpoints
{
    public static IEndpointRouteBuilder MapDriveSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/drive-sync/download/{fileId}", async (
                string fileId,
                HttpContext http,
                [FromServices] IDriveSyncService syncService,
                [FromServices] IGoogleDriveBackupGateway gateway,
                [FromServices] IOptions<DriveSyncGoogleOptions> opts,
                CancellationToken ct) =>
            {
                if (!opts.Value.Enabled)
                    return Results.Json(new { error = "Drive sync is disabled." }, statusCode: StatusCodes.Status503ServiceUnavailable);

                var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? http.User.FindFirstValue("sub") ?? "";

                if (string.IsNullOrEmpty(userId))
                    return Results.Unauthorized();

                var cfg = await syncService.GetUserConfigAsync(userId, ct).ConfigureAwait(false);
                if (cfg is not { IsActive: true })
                    return Results.Forbid();

                if (!await gateway.IsFileInUserBackupTreeAsync(fileId, cfg.TargetFolderId, ct).ConfigureAwait(false))
                    return Results.NotFound();

                var meta = await gateway.GetBackupFileForDownloadMetadataAsync(fileId, ct).ConfigureAwait(false);
                if (meta is null)
                    return Results.NotFound();

                var (fileName, mime) = meta.Value;
                return Results.Stream(
                    async stream => await gateway.StreamBackupFileToAsync(fileId, stream, ct).ConfigureAwait(false),
                    mime,
                    fileDownloadName: fileName);
            })
            .RequireAuthorization()
            .WithName("DriveSyncDownload")
            .WithTags("DriveSync");

        app.MapPost("/api/drive-sync/prepare-upload", async (
                DriveSyncPrepareUploadRequest request,
                HttpContext http,
                [FromServices] IDriveSyncService syncService,
                [FromServices] IOptions<DriveSyncGoogleOptions> opts,
                CancellationToken ct) =>
            {
                if (!opts.Value.Enabled)
                    return Results.Json(new { error = "Drive sync is disabled." }, statusCode: StatusCodes.Status503ServiceUnavailable);

                var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? http.User.FindFirstValue("sub") ?? "";
                if (string.IsNullOrEmpty(userId))
                    return Results.Unauthorized();

                if (request is null || string.IsNullOrWhiteSpace(request.FileName) || request.FileSizeBytes < 0)
                    return Results.BadRequest(new { error = "fileName and fileSizeBytes are required." });

                try
                {
                    var session = await syncService.PrepareUploadSessionAsync(
                        userId,
                        request.RelativePath ?? string.Empty,
                        request.FileName.Trim(),
                        request.FileSizeBytes,
                        request.MimeType,
                        ct).ConfigureAwait(false);
                    return Results.Ok(session);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .RequireAuthorization()
            .WithName("DriveSyncPrepareUpload")
            .WithTags("DriveSync");

        app.MapPost("/api/drive-sync/upload-proxy-chunk", async (
                HttpContext http,
                [FromServices] IHttpClientFactory httpClientFactory,
                CancellationToken ct) =>
            {
                var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? http.User.FindFirstValue("sub") ?? "";
                if (string.IsNullOrEmpty(userId))
                    return Results.Unauthorized();

                var uploadUrl = http.Request.Headers["X-Upload-Url"].ToString();
                var contentRange = http.Request.Headers["X-Content-Range"].ToString();
                if (string.IsNullOrWhiteSpace(uploadUrl) || string.IsNullOrWhiteSpace(contentRange))
                    return Results.BadRequest(new { error = "X-Upload-Url and X-Content-Range headers are required." });

                byte[] payload;
                await using (var ms = new MemoryStream())
                {
                    await http.Request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
                    payload = ms.ToArray();
                }

                using var req = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                req.Headers.TryAddWithoutValidation("Content-Range", contentRange);
                req.Content = new ByteArrayContent(payload);
                req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                using var client = httpClientFactory.CreateClient();
                using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var range = resp.Headers.TryGetValues("Range", out var values) ? values.FirstOrDefault() : null;

                return Results.Json(
                    new
                    {
                        status = (int)resp.StatusCode,
                        range,
                        body = body.Length > 800 ? body[..800] : body
                    },
                    statusCode: (int)resp.StatusCode);
            })
            .RequireAuthorization()
            .WithName("DriveSyncUploadProxyChunk")
            .WithTags("DriveSync");

        app.MapPost("/api/drive-sync/delete-file-by-path", async (
                DriveSyncDeleteByPathRequest request,
                HttpContext http,
                [FromServices] IDriveSyncService syncService,
                [FromServices] IGoogleDriveBackupGateway gateway,
                [FromServices] IOptions<DriveSyncGoogleOptions> opts,
                CancellationToken ct) =>
            {
                if (!opts.Value.Enabled)
                    return Results.Json(new { error = "Drive sync is disabled." }, statusCode: StatusCodes.Status503ServiceUnavailable);
                var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? http.User.FindFirstValue("sub") ?? "";
                if (string.IsNullOrWhiteSpace(userId))
                    return Results.Unauthorized();
                if (request is null || string.IsNullOrWhiteSpace(request.FileName))
                    return Results.BadRequest(new { error = "fileName is required." });
                var cfg = await syncService.GetUserConfigAsync(userId, ct).ConfigureAwait(false);
                if (cfg is not { IsActive: true })
                    return Results.Forbid();
                var deleted = await gateway.DeleteFileByPathAsync(
                    cfg.TargetFolderId,
                    request.RelativePath ?? string.Empty,
                    request.FileName.Trim(),
                    ct).ConfigureAwait(false);
                return Results.Ok(new { deleted });
            })
            .RequireAuthorization()
            .WithName("DriveSyncDeleteFileByPath")
            .WithTags("DriveSync");

        app.MapGet("/api/drive-sync/oauth/start", async (
                HttpContext http,
                [FromServices] IDriveSyncOAuthService oauthService,
                CancellationToken ct) =>
            {
                if (!AdminAuthorization.IsAdministrator(http.User))
                    return Results.Forbid();
                var adminUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? http.User.FindFirstValue("sub")
                                  ?? "admin";
                var url = await oauthService.GetAuthorizationUrlAsync(adminUserId, ct).ConfigureAwait(false);
                return Results.Ok(new { authorizationUrl = url });
            })
            .RequireAuthorization()
            .WithName("DriveSyncOAuthStart")
            .WithTags("DriveSync");

        app.MapGet("/api/drive-sync/oauth/status", async (
                HttpContext http,
                [FromServices] IDriveSyncOAuthService oauthService,
                CancellationToken ct) =>
            {
                if (!AdminAuthorization.IsAdministrator(http.User))
                    return Results.Forbid();
                var status = await oauthService.GetStatusAsync(ct).ConfigureAwait(false);
                return Results.Ok(status);
            })
            .RequireAuthorization()
            .WithName("DriveSyncOAuthStatus")
            .WithTags("DriveSync");

        app.MapGet("/api/drive-sync/oauth/callback", async (
                string? code,
                string? state,
                string? error,
                [FromServices] IDriveSyncOAuthService oauthService,
                CancellationToken ct) =>
            {
                if (!string.IsNullOrWhiteSpace(error))
                    return Results.Content($"<html><body><h3>DriveSync OAuth failed</h3><p>{System.Net.WebUtility.HtmlEncode(error)}</p></body></html>", "text/html");
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                    return Results.BadRequest(new { error = "Missing code/state in OAuth callback." });

                try
                {
                    await oauthService.HandleOAuthCallbackAsync(code, state, ct).ConfigureAwait(false);
                    return Results.Content("""
<html>
  <body style="font-family:Segoe UI;padding:20px">
    <h3>DriveSync OAuth connected</h3>
    <p>You can close this window and return to Tyresoles Users page.</p>
    <script>setTimeout(function(){ window.close(); }, 1500);</script>
  </body>
</html>
""", "text/html");
                }
                catch (Exception ex)
                {
                    return Results.Content($"<html><body><h3>DriveSync OAuth error</h3><p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>", "text/html");
                }
            })
            .AllowAnonymous()
            .WithName("DriveSyncOAuthCallback")
            .WithTags("DriveSync");

        return app;
    }
}

public sealed class DriveSyncPrepareUploadRequest
{
    public string? RelativePath { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? MimeType { get; set; }
}

public sealed class DriveSyncDeleteByPathRequest
{
    public string? RelativePath { get; set; }
    public string FileName { get; set; } = string.Empty;
}
