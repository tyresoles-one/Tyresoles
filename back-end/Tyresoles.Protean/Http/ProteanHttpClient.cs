using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tyresoles.Protean;

namespace Tyresoles.Protean.Http;

/// <summary>
/// Shared async HTTP helper. Handles:
/// • Building query-strings for GET
/// • Attaching headers
/// • JSON serialise / deserialise (System.Text.Json)
/// • Structured logging of round-trip
/// </summary>
internal abstract class ProteanHttpClientBase
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new LongToStringConverter() },
    };

    protected readonly HttpClient Http;
    protected readonly ILogger Logger;

    protected ProteanHttpClientBase(HttpClient http, ILogger logger)
    {
        Http   = http;
        Logger = logger;
    }

    protected async Task<TResponse?> PostInternalAsync<TResponse>(
        string url, object body, Dictionary<string, string> headers, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        ApplyHeaders(req, headers);

        Logger.LogDebug("POST {Url}", url);
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var rawJson = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Logger.LogInformation("Protean Raw Response (POST {Url}): {RawJson}", url, rawJson);

        if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new ProteanException($"Protean auth returned 403 Forbidden for {url}. Production often requires whitelisting your server IP with the GSP; for testing set Constants.Environment = ProteanEnvironment.Sandbox or env PROTEAN_SANDBOX=true.");
        
        resp.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<TResponse>(rawJson, _jsonOpts);
    }

    protected async Task<TResponse?> GetInternalAsync<TResponse>(
        string url, Dictionary<string, string>? query, Dictionary<string, string> headers, CancellationToken ct)
    {
        var fullUrl = query is { Count: > 0 }
            ? $"{url}?{string.Join('&', query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"))}"
            : url;

        using var req = new HttpRequestMessage(HttpMethod.Get, fullUrl);
        ApplyHeaders(req, headers);

        Logger.LogDebug("GET {Url}", fullUrl);
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var rawJson = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Logger.LogInformation("Protean Raw Response (GET {Url}): {RawJson}", fullUrl, rawJson);

        if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new ProteanException($"Protean API returned 403 Forbidden for {fullUrl}. If using production, ensure your server IP is whitelisted with the GSP; for testing use Sandbox (Constants.Environment or PROTEAN_SANDBOX=true).");
        
        resp.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<TResponse>(rawJson, _jsonOpts);
    }

    private static void ApplyHeaders(HttpRequestMessage req, Dictionary<string, string> headers)
    {
        foreach (var (k, v) in headers)
            if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                req.Headers.TryAddWithoutValidation(k, v);
    }
}

// ──────────────────────────────────────────────────────────────────
// Concrete named implementations
// ──────────────────────────────────────────────────────────────────

internal sealed class ProteanHttpClient(HttpClient http, ILogger<ProteanHttpClient> log)
    : ProteanHttpClientBase(http, log), IProteanHttpClient
{
    public Task<T?> PostAsync<T>(string url, object body, Dictionary<string, string> headers, CancellationToken ct = default)
        => PostInternalAsync<T>(url, body, headers, ct);

    public Task<T?> GetAsync<T>(string url, Dictionary<string, string>? query, Dictionary<string, string> headers, CancellationToken ct = default)
        => GetInternalAsync<T>(url, query, headers, ct);
}

internal sealed class ProteanEWbHttpClient(HttpClient http, ILogger<ProteanEWbHttpClient> log)
    : ProteanHttpClientBase(http, log), IProteanEWbHttpClient
{
    public Task<T?> PostAsync<T>(string url, object body, Dictionary<string, string> headers, CancellationToken ct = default)
        => PostInternalAsync<T>(url, body, headers, ct);

    public Task<T?> GetAsync<T>(string url, Dictionary<string, string>? query, Dictionary<string, string> headers, CancellationToken ct = default)
        => GetInternalAsync<T>(url, query, headers, ct);
}

// ──────────────────────────────────────────────────────────────────
// Concrete named implementations
// ──────────────────────────────────────────────────────────────────
