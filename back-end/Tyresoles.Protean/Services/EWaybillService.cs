using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tyresoles.Protean.Encryption;
using Tyresoles.Protean.Http;
using Tyresoles.Protean.Models.EWaybill;
using Tyresoles.Protean.Session;

namespace Tyresoles.Protean.Services;

/// <summary>
/// EWaybill service implementation (Protean GSP API v1.03).
/// Async-first, session-managed, no static state.
/// </summary>
internal sealed class EWaybillService(
    IProteanEWbHttpClient     client,
    IProteanSessionService    sessions,
    IProteanEncryptor         encryptor,
    ILogger<EWaybillService>  logger)
    : IEWaybillService
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new LongToStringConverter() },
    };

    // ──────────────────────────────────────────────────────────────
    // Generate EWaybill
    // ──────────────────────────────────────────────────────────────
    public async Task<EWaybillGenResult> GenerateAsync(EWaybillRequest request, CancellationToken ct = default)
    {
        if (request.Request is null || request.Request is not EWaybillGeneratePayload payload)
            throw new ProteanException("Generate payload (Request) is required.");

        logger.LogInformation("request {req}", request.ToString());

        var session = await sessions.GetEWaybillSessionAsync(request.Gstin, request.UserName, request.Password, ct)
                                    .ConfigureAwait(false);

        var body = BuildActionBody(EWaybillActions.Generate, payload, session.Sek);
        var headers = BuildHeaders(request.Gstin, session.AuthToken, "EWB");

        logger.LogInformation("GenerateEWaybill {DocNo}", payload.docNo);

        var resp = await client.PostAsync<EwbResponse>(
            EWaybillUrls.Main(Constants.Sandbox), body, headers, ct).ConfigureAwait(false);

        return DecryptEwbResponse<EWaybillGenResult>(resp, session.Sek, payload.docNo);
    }

    // ──────────────────────────────────────────────────────────────
    // Part B / vehicle update
    // ──────────────────────────────────────────────────────────────
    public async Task<EWaybillPartBResult> UpdatePartBAsync(EWaybillRequest request, CancellationToken ct = default)
    {
        if (request.Request is null || request.Request is not EWaybillPartBPayload payload)
            throw new ProteanException("PartB payload (Request) is required.");

        var session = await sessions.GetEWaybillSessionAsync(request.Gstin, request.UserName, request.Password, ct)
                                    .ConfigureAwait(false);

        var body = BuildActionBody(EWaybillActions.GenPartB, payload, session.Sek);
        var headers = BuildHeaders(request.Gstin, session.AuthToken, "UPDATEVEHICLE");

        var resp = await client.PostAsync<EwbResponse>(
            EWaybillUrls.Main(Constants.Sandbox), body, headers, ct).ConfigureAwait(false);

        return DecryptEwbResponse<EWaybillPartBResult>(resp, session.Sek, payload.ewbNo.ToString());
    }

    // ──────────────────────────────────────────────────────────────
    // Consolidate
    // ──────────────────────────────────────────────────────────────
    public async Task<EWaybillConsolidateResult> GenerateConsolidateAsync(EWaybillRequest request, CancellationToken ct = default)
    {
        if (request.Request is null || request.Request is not EWaybillConsolidatePayload payload)
            throw new ProteanException("Consolidate payload (Request) is required.");

        var session = await sessions.GetEWaybillSessionAsync(request.Gstin, request.UserName, request.Password, ct)
                                    .ConfigureAwait(false);

        var body = BuildActionBody(EWaybillActions.GenConsolidate, payload, session.Sek);
        var headers = BuildHeaders(request.Gstin, session.AuthToken, "CONSOLIDATEDEWB");

        var resp = await client.PostAsync<EwbResponse>(
            EWaybillUrls.Main(Constants.Sandbox), body, headers, ct).ConfigureAwait(false);

        return DecryptEwbResponse<EWaybillConsolidateResult>(resp, session.Sek, "Consolidate");
    }

    // ──────────────────────────────────────────────────────────────
    // Cancel
    // ──────────────────────────────────────────────────────────────
    public async Task<EWaybillCancelResult> CancelAsync(EWaybillRequest request, CancellationToken ct = default)
    {
        if (request.Request is null || request.Request is not EWaybillCancelPayload payload)
            throw new ProteanException("Cancel payload (Request) is required.");

        var session = await sessions.GetEWaybillSessionAsync(request.Gstin, request.UserName, request.Password, ct)
                                    .ConfigureAwait(false);

        var body = BuildActionBody(EWaybillActions.Cancel, payload, session.Sek);
        var headers = BuildHeaders(request.Gstin, session.AuthToken, "CANCELEWB");

        var resp = await client.PostAsync<EwbResponse>(
            EWaybillUrls.Main(Constants.Sandbox), body, headers, ct).ConfigureAwait(false);

        return DecryptEwbResponse<EWaybillCancelResult>(resp, session.Sek, payload.ewbNo.ToString());
    }

    // ──────────────────────────────────────────────────────────────
    // Get by document number
    // ──────────────────────────────────────────────────────────────
    public async Task<EWaybillGenResult> GetByDocumentAsync(EWaybillRequest request, CancellationToken ct = default)
    {
        if (request.Request is null || request.Request is not EWaybillByDocPayload payload)
            throw new ProteanException("ByDoc payload (Request) is required.");

        var session = await sessions.GetEWaybillSessionAsync(request.Gstin, request.UserName, request.Password, ct)
                                    .ConfigureAwait(false);

        var query = new Dictionary<string, string>
        {
            ["docType"] = payload.docType,
            ["docNo"]   = payload.docNo,
        };
        var headers = BuildHeaders(request.Gstin, session.AuthToken, "EWB");

        logger.LogInformation("GetEWaybillByDoc {DocNo}", payload.docNo);

        var resp = await client.GetAsync<EwbGetByDocResponse>(
            EWaybillUrls.GetEwbByDoc(Constants.Sandbox), query, headers, ct).ConfigureAwait(false);

        if (resp is null || resp.status != "1" || string.IsNullOrEmpty(resp.data))
            throw new ProteanException($"GetByDocument failed for {payload.docNo}");

        // Same as Tyresoles.Live Processor.GetEwaybillByDocNo — Encryptor.DecryptBySymmerticKey returns
        // Base64(plainBytes), not UTF-8 text (see Utilities.Encryptor.DecryptBySymmerticKey string overload).
        var sekBytes = Convert.FromBase64String(session.Sek);
        string rek = Convert.ToBase64String(encryptor.DecryptAesBytes(resp.rek!, sekBytes));
        string data = Convert.ToBase64String(encryptor.DecryptAesBytes(resp.data!, Convert.FromBase64String(rek)));
        string respData = Encoding.UTF8.GetString(Convert.FromBase64String(data));

        return JsonSerializer.Deserialize<EWaybillGenResult>(respData, _jsonOpts)
               ?? throw new ProteanException("Failed to deserialize GetByDocument response.");
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────
    private object BuildActionBody(string action, object payload, string sekBase64)
    {
        var sekBytes  = Convert.FromBase64String(sekBase64);
        var encrypted = encryptor.EncryptAes(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)), sekBytes);
        return new { action, data = encrypted };
    }

    private Dictionary<string, string> BuildHeaders(string gstin, string authToken, string action = "")
    {
        var (basicKey, basicVal) = ProteanSessionService.BuildBasicToken(gstin, action);
        var signature = encryptor.SignData(Encoding.UTF8.GetBytes(basicVal), Constants.PrivateKeyXml);

        return new Dictionary<string, string>
        {
            ["Content-Type"]         = "application/json",
            ["gstin"]                = gstin,
            ["authtoken"]            = authToken,
            [basicKey]               = basicVal,
            ["X-Asp-Auth-Signature"] = signature,
        };
    }

    private TResult DecryptEwbResponse<TResult>(EwbResponse? resp, string sekBase64, string label)
    {
        if (resp is null)
            throw new ProteanException($"Null response for {label}");

        if (resp.status == "0" && !string.IsNullOrEmpty(resp.error))
        {
            // Parse error codes from the error payload
            try
            {
                var errDecoded = encryptor.Base64Decode(resp.error);
                var errObj     = JsonSerializer.Deserialize<EwbErrorResponse>(errDecoded, _jsonOpts);
                if (errObj?.errorCodes is not null)
                {
                    var errors = BuildErrorDict(errObj.errorCodes);
                    var ex     = new EWaybillGenerateException($"EWaybill error for {label}", errors);
                    logger.LogWarning(ex, "Protean e-waybill rejected {Label}: {Detail}", label, ex.Message);
                    throw ex;
                }
            }
            catch (EWaybillGenerateException) { throw; }
            catch { /* fall through to generic */ }
            throw new ProteanException($"EWaybill {label}: {resp.error}");
        }

        if (resp.status != "1" || string.IsNullOrEmpty(resp.data))
            throw new ProteanException($"{label}: Unexpected EWaybill response status={resp.status}");

        var bytes = encryptor.DecryptAesBytes(resp.data!, Convert.FromBase64String(sekBase64));
        return JsonSerializer.Deserialize<TResult>(bytes, _jsonOpts)
               ?? throw new ProteanException($"{label}: Failed to deserialize EWaybill response.");
    }

    private static Dictionary<string, string> BuildErrorDict(string codes)
    {
        var result    = new Dictionary<string, string>();
        foreach (var code in codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var msg = EwbErrorMessages.Get(code);
            result[code] = msg;
        }
        return result;
    }

    // ──────────────────────────────────────────────────────────────
    // Wire DTOs (private, not part of public API)
    // ──────────────────────────────────────────────────────────────
    private sealed class EwbResponse
    {
        public string? status { get; set; }
        public string? data   { get; set; }
        public string? error  { get; set; }
        public string? alert  { get; set; }
        public string? info   { get; set; }
    }
    private sealed class EwbGetByDocResponse
    {
        public string? status { get; set; }
        public string? data   { get; set; }
        public string? rek    { get; set; }
        public string? hmac   { get; set; }
    }
    private sealed class EwbErrorResponse
    {
        public string? errorCodes { get; set; }
    }
}
