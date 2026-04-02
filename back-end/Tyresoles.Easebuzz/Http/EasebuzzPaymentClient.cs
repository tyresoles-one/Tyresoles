using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Easebuzz.Models;

namespace Tyresoles.Easebuzz.Http;

/// <summary>
/// Typed HTTP client for Easebuzz Initiate Payment and Transaction APIs.
/// Endpoint paths per https://docs.easebuzz.in/docs/payment-gateway/
/// </summary>
public sealed class EasebuzzPaymentClient : IEasebuzzPaymentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger<EasebuzzPaymentClient> _logger;
    private readonly EasebuzzOptions _options;

    public EasebuzzPaymentClient(HttpClient http, IOptions<EasebuzzOptions> options, ILogger<EasebuzzPaymentClient> logger)
    {
        _http = http;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<InitiatePaymentResult?> InitiatePaymentAsync(InitiatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        const string path = "payment/initiateLink";
        _logger.LogDebug("POST {Path}", path);

        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json"),
        };

        using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<InitiatePaymentResult>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TransactionStatusResult?> GetTransactionStatusAsync(string txnIdOrRef, CancellationToken cancellationToken = default)
    {
        var path = $"transaction/retrieve?txnid={Uri.EscapeDataString(txnIdOrRef)}";
        _logger.LogDebug("GET {Path}", path);

        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return null;
        return await resp.Content.ReadFromJsonAsync<TransactionStatusResult>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
