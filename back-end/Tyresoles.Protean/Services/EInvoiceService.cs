using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tyresoles.Protean.Encryption;
using Tyresoles.Protean.Http;
using Tyresoles.Protean.Models.EInvoice;
using Tyresoles.Protean.Models.Gstin;
using Tyresoles.Protean.Session;

namespace Tyresoles.Protean.Services;

/// <summary>
/// EInvoice service implementation (IRP API v1.04 via Protean GSP).
/// Async-first, no static state; session management delegated to <see cref="IProteanSessionService"/>.
/// </summary>
internal sealed class EInvoiceService(
    IProteanHttpClient        client,
    IProteanSessionService    sessions,
    IProteanEncryptor         encryptor,
    ILogger<EInvoiceService>  logger)
    : IEInvoiceService
{
    // ──────────────────────────────────────────────────────────────
    // Generate IRN
    // ──────────────────────────────────────────────────────────────
    public async Task<EInvoiceResult> GenerateIrnAsync(EInvoiceRequest request, CancellationToken ct = default)
    {
        var session = await sessions.GetEInvoiceSessionAsync(request.Gstin, request.UserName, request.Password, ct)
                                    .ConfigureAwait(false);

        var sekBytes = Convert.FromBase64String(session.Sek);
        var payload  = encryptor.EncryptAes(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request.Request)), sekBytes);

        var body    = new { Data = payload };
        var headers = BuildHeaders(request.Gstin, session.AuthToken, session.Username, "EInvoice");

        logger.LogInformation("GenerateIRN {Doc}", request.DocumentLabel());

        var resp = await client.PostAsync<EInvPackResponse>(
            EInvoiceUrls.GenerateEInvoice(Constants.Sandbox), body, headers, ct).ConfigureAwait(false);

        return DecryptInvoiceResponse(resp, session.Sek, request.DocumentLabel());
    }

    // ──────────────────────────────────────────────────────────────
    // Get IRN by hash
    // ──────────────────────────────────────────────────────────────
    public async Task<EInvoiceResult> GetIrnAsync(EInvoiceRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Irn))
            throw new ProteanException("IRN is required for GetIrn.");

        var session = await sessions.GetEInvoiceSessionAsync(request.Gstin, request.UserName, request.Password, ct)
                                    .ConfigureAwait(false);

        var headers = BuildHeaders(request.Gstin, session.AuthToken, session.Username, "EInvoice");

        logger.LogInformation("GetIRN {Irn}", request.Irn);

        var resp = await client.GetAsync<EInvPackResponse>(
            EInvoiceUrls.GetEInvoice(Constants.Sandbox, request.Irn), null, headers, ct).ConfigureAwait(false);

        return DecryptInvoiceResponse(resp, session.Sek, request.Irn ?? "");
    }

    // ──────────────────────────────────────────────────────────────
    // Get IRN by document details
    // ──────────────────────────────────────────────────────────────
    public async Task<EInvoiceResult> GetIrnByDocumentAsync(EInvoiceRequest request, CancellationToken ct = default)
    {
        if (request.Document is null)
            throw new ProteanException("Document details are required.");
        if ((DateTime.UtcNow - request.Document.Date.ToUniversalTime()).TotalDays > 2)
            throw new ProteanException("Only records within the last 2 days are allowed.");

        var session = await sessions.GetEInvoiceSessionAsync(request.Gstin, request.UserName, request.Password, ct)
                                    .ConfigureAwait(false);

        var query = new Dictionary<string, string>
        {
            ["doctype"] = request.Document.Type,
            ["docnum"]  = request.Document.No,
            ["docdate"] = request.Document.Date.ToString("dd'/'MM'/'yyyy"),
        };

        var headers = BuildHeaders(request.Gstin, session.AuthToken, session.Username, "EInvoice");

        logger.LogInformation("GetIRNByDoc {Doc}", request.DocumentLabel());

        var resp = await client.GetAsync<EInvPackResponse>(
            EInvoiceUrls.GetIRNByDocument(Constants.Sandbox), query, headers, ct).ConfigureAwait(false);

        return DecryptInvoiceResponse(resp, session.Sek, request.DocumentLabel());
    }

    // ──────────────────────────────────────────────────────────────
    // GSTIN lookup
    // ──────────────────────────────────────────────────────────────
    public async Task<GstinDetails?> GetGstinAsync(string searchGstin, CancellationToken ct = default)
        => await FetchGstinAsync(EInvoiceUrls.GetGSTIN(Constants.Sandbox, searchGstin), ct).ConfigureAwait(false);

    public async Task<GstinDetails?> SyncGstinAsync(string searchGstin, CancellationToken ct = default)
        => await FetchGstinAsync(EInvoiceUrls.SyncGSTIN(Constants.Sandbox, searchGstin), ct).ConfigureAwait(false);

    private async Task<GstinDetails?> FetchGstinAsync(string url, CancellationToken ct)
    {
        var session = await sessions.GetEInvoiceSessionAsync(
            Constants.DefaultGstin, Constants.DefaultEInvUsername, Constants.DefaultEInvPassword, ct).ConfigureAwait(false);

        System.Console.WriteLine(session);
        var headers = BuildHeaders(Constants.DefaultGstin, session.AuthToken, session.Username, "Master");

        var resp = await client.GetAsync<EInvGenericResponse>(url, null, headers, ct).ConfigureAwait(false);
        if (resp is null || resp.Status != 1 || string.IsNullOrEmpty(resp.Data))
            return null;

        byte[] sekBytes;
        try
        {
            sekBytes = Convert.FromBase64String(session.Sek);
        }
        catch (FormatException)
        {
            // Stale cache: session was stored with old format (UTF-8 string instead of Base64). Invalidate and refetch.
            sessions.InvalidateEInvoiceSession(Constants.DefaultGstin);
            session = await sessions.GetEInvoiceSessionAsync(Constants.DefaultGstin, Constants.DefaultEInvUsername, Constants.DefaultEInvPassword, ct).ConfigureAwait(false);
            sekBytes = Convert.FromBase64String(session.Sek);
        }

        var decrypted = encryptor.DecryptAesBytes(resp.Data, sekBytes);
        return JsonSerializer.Deserialize<GstinDetails>(decrypted, _jsonOpts);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────
    private Dictionary<string, string> BuildHeaders(string gstin, string authToken, string username, string action = "")
    {
        var (basicKey, basicVal) = ProteanSessionService.BuildBasicToken(gstin, action);
        var signature = encryptor.SignData(Encoding.UTF8.GetBytes(basicVal), Constants.PrivateKeyXml);

        return new Dictionary<string, string>
        {
            ["Content-Type"]         = "application/json",
            ["Gstin"]                = gstin,
            ["authtoken"]            = authToken,
            ["user_name"]            = username,
            [basicKey]               = basicVal,
            ["X-Asp-Auth-Signature"] = signature,
        };
    }

    private EInvoiceResult DecryptInvoiceResponse(EInvPackResponse? resp, string sekBase64, string label)
    {
        if (resp is null)
            throw new ProteanException($"Null response for {label}");

        if (resp.Status == 0 && resp.ErrorDetails?.Length > 0)
        {
            var first = resp.ErrorDetails[0];
            if (first.ErrorCode == "2150" && resp.InfoDtls?.Length > 0)
            {
                var info = resp.InfoDtls[0];
                throw new DuplicateIrnException(
                    first.ToString() ?? "Duplicate IRN",
                    new DuplicateIrnInfo(info.Desc?.AckNo, info.Desc?.AckDt, info.Desc?.Irn));
            }
            throw new ProteanException($"{label}: {resp.ErrorDetails[0]}");
        }

        if (resp.Status != 1 || string.IsNullOrEmpty(resp.Data))
            throw new ProteanException($"{label}: Unexpected response status={resp.Status}");

        var bytes = encryptor.DecryptAesBytes(resp.Data, Convert.FromBase64String(sekBase64));
        return JsonSerializer.Deserialize<EInvoiceResult>(bytes, _jsonOpts)
               ?? throw new ProteanException("Failed to deserialize EInvoice response.");
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new LongToStringConverter() },
    };

    // ──────────────────────────────────────────────────────────────
    // Internal API wire DTOs
    // ──────────────────────────────────────────────────────────────
    private sealed class EInvPackResponse
    {
        public int Status { get; set; }
        public string? Data { get; set; }
        public EInvError[]? ErrorDetails { get; set; }
        public EInvInfo[]? InfoDtls { get; set; }
    }
    private sealed class EInvGenericResponse
    {
        public int Status { get; set; }
        public string? Data { get; set; }
        public EInvError[]? ErrorDetails { get; set; }
    }
    private sealed class EInvError
    {
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public override string ToString() => $"{ErrorCode}: {ErrorMessage}";
    }
    private sealed class EInvInfo
    {
        public string? InfCd { get; set; }
        public EInvInfoDesc? Desc { get; set; }
    }
    private sealed class EInvInfoDesc
    {
        public string? AckNo { get; set; }
        public string? AckDt { get; set; }
        public string? Irn   { get; set; }
    }
}
