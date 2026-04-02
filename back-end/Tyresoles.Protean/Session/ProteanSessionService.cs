using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Tyresoles.Protean.Encryption;
using Tyresoles.Protean.Http;

namespace Tyresoles.Protean.Session;

/// <summary>
/// Manages Protean GSP sessions for both EInvoice and EWaybill APIs.
/// Sessions are cached in-memory keyed by (gstin + apiKind).
/// Thread-safe via IMemoryCache.
/// </summary>
public interface IProteanSessionService
{
    Task<ProteanSession> GetEInvoiceSessionAsync(string gstin, string username, string password, CancellationToken ct = default);
    Task<ProteanSession> GetEWaybillSessionAsync(string gstin, string username, string password, CancellationToken ct = default);
    Task<ProteanSession> RefreshEWaybillSessionAsync(string gstin, string username, string password, CancellationToken ct = default);
    /// <summary>Removes cached EInvoice session for the given GSTIN so the next request fetches a fresh session.</summary>
    void InvalidateEInvoiceSession(string gstin);

    /// <summary>Removes cached E-Waybill session for the given GSTIN (mirrors Live RemoveExpiredSession on invalid token).</summary>
    void InvalidateEWaybillSession(string gstin);
}

internal sealed class ProteanSessionService(
    IProteanHttpClient    einvoiceClient,
    IProteanEWbHttpClient ewbClient,
    IProteanEncryptor     encryptor,
    IMemoryCache          cache,
    ILogger<ProteanSessionService> logger)
    : IProteanSessionService
{
    private static string EInvKey(string gstin) => $"protean:einv:{gstin}";
    private static string EWbKey(string gstin)  => $"protean:ewb:{gstin}";

    // ──────────────────────────────────────────────────────────────
    // EInvoice session (v1.04)
    // ──────────────────────────────────────────────────────────────
    public async Task<ProteanSession> GetEInvoiceSessionAsync(
        string gstin, string username, string password, CancellationToken ct = default)
    {
        var key = EInvKey(gstin);
        if (cache.TryGetValue(key, out ProteanSession? cached) && cached!.IsAlive)
            return cached;

        var session = await FetchEInvoiceSessionAsync(gstin, username, password, ct)
                           .ConfigureAwait(false);
        cache.Set(key, session, session.ExpiresAt - DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5));
        return session;
    }

    public void InvalidateEInvoiceSession(string gstin) => cache.Remove(EInvKey(gstin));

    public void InvalidateEWaybillSession(string gstin) => cache.Remove(EWbKey(gstin));

    // ──────────────────────────────────────────────────────────────
    // EWaybill session (v1.03)
    // ──────────────────────────────────────────────────────────────
    public async Task<ProteanSession> GetEWaybillSessionAsync(
        string gstin, string username, string password, CancellationToken ct = default)
    {
        var key = EWbKey(gstin);
        if (cache.TryGetValue(key, out ProteanSession? cached) && cached!.IsAlive)
            return cached;

        return await RefreshEWaybillSessionAsync(gstin, username, password, ct).ConfigureAwait(false);
    }

    public async Task<ProteanSession> RefreshEWaybillSessionAsync(
        string gstin, string username, string password, CancellationToken ct = default)
    {
        var session = await FetchEWaybillSessionAsync(gstin, username, password, ct).ConfigureAwait(false);
        cache.Set(EWbKey(gstin), session, session.ExpiresAt - DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5));
        return session;
    }

    // ──────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────
    private async Task<ProteanSession> FetchEInvoiceSessionAsync(
        string gstin, string username, string password, CancellationToken ct)
    {
        logger.LogInformation("Fetching fresh EInvoice session for GSTIN: {Gstin}", gstin);

        var appKeyBytes = encryptor.GenerateSecureKey();
        logger.LogInformation("Step 1: Generated AppKey (Base64): {AppKey}", Convert.ToBase64String(appKeyBytes));

        var authReq  = new { UserName = username, Password = password, AppKey = Convert.ToBase64String(appKeyBytes) };
        var reqBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(authReq));
        var encryptedAuthData = encryptor.EncryptRsa(Convert.ToBase64String(reqBytes), Constants.PublicKeyNicIrp);
        var body     = new { Data = encryptedAuthData };
        logger.LogInformation("Step 2: Encrypted Auth Request Data prepared");

        var (basicKey, basicVal) = BuildBasicToken(gstin, string.Empty);
        logger.LogInformation("Step 3: Basic Token prepared: {Token}", basicVal);
        
        var signedVal = encryptor.SignData(Encoding.UTF8.GetBytes(basicVal), Constants.PrivateKeyXml);
        logger.LogInformation("Step 4: Basic Token signed successfully");

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"]        = "application/json",
            ["Gstin"]               = gstin,
            [basicKey]              = basicVal,
            ["X-Asp-Auth-Signature"] = signedVal,
        };

        var url = EInvoiceUrls.Authentication(Constants.Sandbox);
        logger.LogInformation("Step 5: Sending POST request to URL: {Url}", url);
        logger.LogInformation("EInvoice Auth Headers: {Headers}", JsonSerializer.Serialize(headers));
        logger.LogInformation("EInvoice Auth Request Body: {Body}", JsonSerializer.Serialize(body));

        var resp = await einvoiceClient.PostAsync<EInvAuthResponse>(url, body, headers, ct).ConfigureAwait(false);

        logger.LogInformation("Step 6: Received Response. Status: {Status}", resp?.Status);
        logger.LogInformation("EInvoice Auth Response Body: {Response}", JsonSerializer.Serialize(resp));

        if (resp is null || resp.Status != 1)
        {
            var error = resp?.ErrorDetails?.FirstOrDefault()?.ToString() ?? "Unknown error (possibly null response or missing error details)";
            logger.LogError("EInvoice Auth Failed for {Gstin}: {Error}", gstin, error);
            throw new ProteanException($"EInvoice auth failed: {error}");
        }

        logger.LogInformation("Step 7: Decrypting SEK from response...");
        // Store Sek as Base64 so consumers can use Convert.FromBase64String(session.Sek) for key bytes.
        var sekBytes = encryptor.DecryptAesBytes(resp.Data!.Sek, appKeyBytes);
        var sekBase64 = Convert.ToBase64String(sekBytes);
        
        DateTimeOffset expiry;
        if (DateTime.TryParse(resp.Data.TokenExpiry, out var parsed))
        {
            // Protean tokens are generally valid for 6 hours. 
            // The string "2026-03-08 20:36:15" doesn't have an offset, assuming IST (+5:30).
            expiry = new DateTimeOffset(parsed, TimeSpan.FromHours(5).Add(TimeSpan.FromMinutes(30)));
        }
        else
        {
            expiry = DateTimeOffset.UtcNow.AddHours(6);
        }

        logger.LogInformation("EInvoice session successfully refreshed for {Gstin}, expires at {ExpiresAt}", gstin, expiry);

        return new ProteanSession
        {
            Gstin     = gstin,
            Username  = username,
            Password  = password,
            AuthToken = resp.Data.AuthToken,
            AppKey    = Convert.ToBase64String(appKeyBytes),
            Sek       = sekBase64,
            ExpiresAt = expiry,
        };
    }

    private async Task<ProteanSession> FetchEWaybillSessionAsync(
        string gstin, string username, string password, CancellationToken ct)
    {
        var appKeyBytes = encryptor.GenerateSecureKey();

        var authReq = new
        {
            action   = "ACCESSTOKEN",
            username,
            password,
            app_key  = Convert.ToBase64String(appKeyBytes),
        };
        var reqBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(authReq));
        var body     = new { Data = encryptor.EncryptRsa(Convert.ToBase64String(reqBytes), Constants.PublicKeyNicIrp) };

        var (basicKey, basicVal) = BuildBasicToken(gstin, string.Empty);
        var signedVal            = encryptor.SignData(Encoding.UTF8.GetBytes(basicVal), Constants.PrivateKeyXml);

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"]         = "application/json",
            ["Gstin"]                = gstin,
            [basicKey]               = basicVal,
            ["X-Asp-Auth-Signature"] = signedVal,
        };

        var resp = await ewbClient.PostAsync<EWbAuthResponse>(
            EWaybillUrls.Authentication(Constants.Sandbox), body, headers, ct).ConfigureAwait(false);

        if (resp is null || resp.status != "1")
            throw new ProteanException($"EWaybill auth failed: {resp?.error}");

        var sekBytes = encryptor.DecryptAesBytes(resp.sek!, appKeyBytes);
        var sekBase64 = Convert.ToBase64String(sekBytes);
        var expiry = DateTimeOffset.UtcNow.AddHours(6);

        logger.LogInformation("EWaybill session refreshed for {Gstin}, expires {ExpiresAt}", gstin, expiry);

        return new ProteanSession
        {
            Gstin     = gstin,
            Username  = username,
            Password  = password,
            AuthToken = resp.authtoken!,
            AppKey    = Convert.ToBase64String(appKeyBytes),
            Sek       = sekBase64,
            ExpiresAt = expiry,
        };
    }

    internal static (string key, string value) BuildBasicToken(string gstin, string action)
    {
        var now = DateTimeOffset.Now;
        var token = Constants.AuthTokenFormat
            .Replace("<asp_id>",    Constants.AspId)
            .Replace("<client_id>", string.Empty)
            .Replace("<txn_id>",    now.ToString("yyyyMMddHHmmss"))
            .Replace("<timestamp>", now.ToString("yyyyMMddHHmmsszzz").Replace(":", string.Empty))
            .Replace("<gstin>",     gstin)
            .Replace("<api_action>", action);
        return ("X-Asp-Auth-Token", token);
    }

    // ──────────────────────────────────────────────────────────────
    // Internal auth response DTOs (not exposed to callers)
    // ──────────────────────────────────────────────────────────────
    private sealed class EInvAuthResponse
    {
        public int Status { get; set; }
        public EInvAuthData? Data { get; set; }
        public EInvError[]? ErrorDetails { get; set; }
    }
    private sealed class EInvAuthData
    {
        public string AuthToken    { get; set; } = "";
        public string Sek          { get; set; } = "";
        public string TokenExpiry  { get; set; } = "";
    }
    private sealed class EInvError
    {
        public string? ErrorCode    { get; set; }
        public string? ErrorMessage { get; set; }
        public override string ToString() => $"{ErrorCode}: {ErrorMessage}";
    }
    private sealed class EWbAuthResponse
    {
        public string? status    { get; set; }
        public string? authtoken { get; set; }
        public string? sek       { get; set; }
        public string? error     { get; set; }
    }
}
