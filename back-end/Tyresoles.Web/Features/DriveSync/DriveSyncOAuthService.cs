using System.Data;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Tyresoles.Data.Features.DriveSync;
using Tyresoles.Data.Features.DriveSync.Entities;

namespace Tyresoles.Web.Features.DriveSync;

public sealed class DriveSyncOAuthService : IDriveSyncOAuthService
{
    private const string TableName = "dbo.DriveSyncOAuthTokens";
    private readonly string _connectionString;
    private readonly DriveSyncGoogleOptions _options;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DriveSyncOAuthService> _log;

    public DriveSyncOAuthService(
        IConfiguration config,
        IOptions<DriveSyncGoogleOptions> options,
        IDataProtectionProvider dp,
        IHttpClientFactory httpClientFactory,
        IDistributedCache cache,
        ILogger<DriveSyncOAuthService> log)
    {
        _connectionString = config.GetConnectionString("Calendar")
            ?? throw new InvalidOperationException("ConnectionStrings:Calendar is required for DriveSync OAuth token storage.");
        _options = options.Value;
        _protector = dp.CreateProtector("Tyresoles.DriveSync.OAuthTokens.v1");
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _log = log;
    }

    public async Task<string> GetAuthorizationUrlAsync(string adminUserId, CancellationToken ct = default)
    {
        EnsureOAuthConfigured();
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await _cache.SetStringAsync(
            $"DriveSync:OAuthState:{state}",
            adminUserId,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
            ct).ConfigureAwait(false);

        var url = "https://accounts.google.com/o/oauth2/v2/auth?"
                  + $"client_id={Uri.EscapeDataString(_options.OAuthClientId)}&"
                  + $"redirect_uri={Uri.EscapeDataString(_options.OAuthRedirectUri)}&"
                  + "response_type=code&"
                  + $"scope={Uri.EscapeDataString("https://www.googleapis.com/auth/drive")}&"
                  + "access_type=offline&"
                  + "prompt=consent&"
                  + $"state={Uri.EscapeDataString(state)}";
        return url;
    }

    public async Task HandleOAuthCallbackAsync(string code, string state, CancellationToken ct = default)
    {
        EnsureOAuthConfigured();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Authorization code is required.");
        if (string.IsNullOrWhiteSpace(state))
            throw new InvalidOperationException("OAuth state is required.");

        var stateKey = $"DriveSync:OAuthState:{state.Trim()}";
        var adminUserId = await _cache.GetStringAsync(stateKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(adminUserId))
            throw new InvalidOperationException("OAuth state is missing or expired. Please start again from users page.");
        await _cache.RemoveAsync(stateKey, ct).ConfigureAwait(false);

        var tokens = await ExchangeCodeForTokensAsync(code.Trim(), ct).ConfigureAwait(false);
        var email = await TryGetGoogleAccountEmailAsync(tokens.AccessToken, ct).ConfigureAwait(false);
        await UpsertTokensAsync(tokens.RefreshToken, tokens.AccessToken, tokens.ExpiresAtUtc, email, adminUserId, ct).ConfigureAwait(false);
    }

    public async Task<DriveSyncOAuthStatus> GetStatusAsync(CancellationToken ct = default)
    {
        EnsureOAuthConfigured();
        var row = await ReadRowAsync(ct).ConfigureAwait(false);
        if (row == null)
            return new DriveSyncOAuthStatus { IsConfigured = false };

        var expiry = row.AccessTokenExpiryUtc;
        return new DriveSyncOAuthStatus
        {
            IsConfigured = !string.IsNullOrWhiteSpace(row.RefreshToken),
            HasRefreshToken = !string.IsNullOrWhiteSpace(row.RefreshToken),
            HasAccessToken = !string.IsNullOrWhiteSpace(row.AccessToken),
            AccessTokenExpiryUtc = expiry,
            IsAccessTokenExpired = !expiry.HasValue || expiry.Value <= DateTime.UtcNow,
            GoogleAccountEmail = row.GoogleAccountEmail,
            UpdatedAtUtc = row.UpdatedAtUtc,
            UpdatedByUserId = row.UpdatedByUserId
        };
    }

    public async Task<string> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        EnsureOAuthConfigured();
        var row = await ReadRowAsync(ct).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("DriveSync OAuth tokens are not configured. Connect admin Google account first.");
        if (string.IsNullOrWhiteSpace(row.RefreshToken))
            throw new InvalidOperationException("DriveSync OAuth refresh token is missing. Reconnect admin Google account.");

        var shouldRefresh = string.IsNullOrWhiteSpace(row.AccessToken)
                            || !row.AccessTokenExpiryUtc.HasValue
                            || row.AccessTokenExpiryUtc.Value <= DateTime.UtcNow.AddMinutes(5);
        if (!shouldRefresh)
            return row.AccessToken!;

        var refreshed = await RefreshAccessTokenAsync(row.RefreshToken, ct).ConfigureAwait(false);
        await UpdateAccessTokenAsync(refreshed.AccessToken, refreshed.ExpiresAtUtc, ct).ConfigureAwait(false);
        return refreshed.AccessToken;
    }

    private void EnsureOAuthConfigured()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Drive sync is disabled.");
        if (string.IsNullOrWhiteSpace(_options.OAuthClientId)
            || string.IsNullOrWhiteSpace(_options.OAuthClientSecret)
            || string.IsNullOrWhiteSpace(_options.OAuthRedirectUri))
            throw new InvalidOperationException("OAuth settings are incomplete. Configure DriveSync:OAuthClientId, OAuthClientSecret, and OAuthRedirectUri.");
    }

    private async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc)> ExchangeCodeForTokensAsync(string code, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var payload = new FormUrlEncodedContent(
        [
            new("code", code),
            new("client_id", _options.OAuthClientId),
            new("client_secret", _options.OAuthClientSecret),
            new("redirect_uri", _options.OAuthRedirectUri),
            new("grant_type", "authorization_code")
        ]);
        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", payload, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google OAuth code exchange failed ({(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var access = root.TryGetProperty("access_token", out var ae) ? ae.GetString() : null;
        var refresh = root.TryGetProperty("refresh_token", out var re) ? re.GetString() : null;
        var expires = root.TryGetProperty("expires_in", out var ee) ? ee.GetInt32() : 0;
        if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) || expires <= 0)
            throw new InvalidOperationException("Google OAuth response is missing required token fields.");

        return (access!, refresh!, DateTime.UtcNow.AddSeconds(expires));
    }

    private async Task<(string AccessToken, DateTime ExpiresAtUtc)> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var payload = new FormUrlEncodedContent(
        [
            new("client_id", _options.OAuthClientId),
            new("client_secret", _options.OAuthClientSecret),
            new("refresh_token", refreshToken),
            new("grant_type", "refresh_token")
        ]);
        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", payload, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google OAuth token refresh failed ({(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var access = root.TryGetProperty("access_token", out var ae) ? ae.GetString() : null;
        var expires = root.TryGetProperty("expires_in", out var ee) ? ee.GetInt32() : 0;
        if (string.IsNullOrWhiteSpace(access) || expires <= 0)
            throw new InvalidOperationException("Google OAuth refresh response is missing required fields.");
        return (access!, DateTime.UtcNow.AddSeconds(expires));
    }

    private async Task<string?> TryGetGoogleAccountEmailAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(req, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to read Google account email from userinfo.");
            return null;
        }
    }

    private async Task UpsertTokensAsync(string refreshToken, string accessToken, DateTime expiryUtc, string? email, string adminUserId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
MERGE {TableName} AS t
USING (SELECT 1 AS [Id]) AS s ON t.[Id] = s.[Id]
WHEN MATCHED THEN
    UPDATE SET
      [RefreshTokenEnc] = @refresh,
      [AccessTokenEnc] = @access,
      [AccessTokenExpiryUtc] = @exp,
      [GoogleAccountEmail] = @email,
      [UpdatedByUserId] = @admin,
      [UpdatedAtUtc] = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT ([Id], [RefreshTokenEnc], [AccessTokenEnc], [AccessTokenExpiryUtc], [GoogleAccountEmail], [UpdatedByUserId], [UpdatedAtUtc])
    VALUES (1, @refresh, @access, @exp, @email, @admin, SYSUTCDATETIME());
""";
        cmd.Parameters.AddWithValue("@refresh", Protect(refreshToken));
        cmd.Parameters.AddWithValue("@access", Protect(accessToken));
        cmd.Parameters.AddWithValue("@exp", expiryUtc);
        cmd.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@admin", adminUserId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task UpdateAccessTokenAsync(string accessToken, DateTime expiryUtc, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
UPDATE {TableName}
SET [AccessTokenEnc] = @access,
    [AccessTokenExpiryUtc] = @exp,
    [UpdatedAtUtc] = SYSUTCDATETIME()
WHERE [Id] = 1;
""";
        cmd.Parameters.AddWithValue("@access", Protect(accessToken));
        cmd.Parameters.AddWithValue("@exp", expiryUtc);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<TokenRow?> ReadRowAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
SELECT TOP (1)
    [RefreshTokenEnc],
    [AccessTokenEnc],
    [AccessTokenExpiryUtc],
    [GoogleAccountEmail],
    [UpdatedByUserId],
    [UpdatedAtUtc]
FROM {TableName}
WHERE [Id] = 1;
""";
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        var refreshEnc = reader.IsDBNull(0) ? null : reader.GetString(0);
        var accessEnc = reader.IsDBNull(1) ? null : reader.GetString(1);
        return new TokenRow
        {
            RefreshToken = UnprotectOrNull(refreshEnc),
            AccessToken = UnprotectOrNull(accessEnc),
            AccessTokenExpiryUtc = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            GoogleAccountEmail = reader.IsDBNull(3) ? null : reader.GetString(3),
            UpdatedByUserId = reader.IsDBNull(4) ? null : reader.GetString(4),
            UpdatedAtUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
        };
    }

    private string Protect(string value) => _protector.Protect(value);
    private string? UnprotectOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try { return _protector.Unprotect(value); }
        catch { return null; }
    }

    private sealed class TokenRow
    {
        public string? RefreshToken { get; init; }
        public string? AccessToken { get; init; }
        public DateTime? AccessTokenExpiryUtc { get; init; }
        public string? GoogleAccountEmail { get; init; }
        public string? UpdatedByUserId { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }
    }
}
