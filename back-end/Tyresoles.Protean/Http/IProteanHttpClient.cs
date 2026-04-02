namespace Tyresoles.Protean.Http;

/// <summary>Typed HTTP client for the Protean EInvoice (IRP) API.</summary>
public interface IProteanHttpClient
{
    Task<TResponse?> PostAsync<TResponse>(string url, object body, Dictionary<string, string> headers, CancellationToken ct = default);
    Task<TResponse?> GetAsync<TResponse>(string url, Dictionary<string, string>? query, Dictionary<string, string> headers, CancellationToken ct = default);
}

/// <summary>Typed HTTP client for the Protean EWaybill API.</summary>
public interface IProteanEWbHttpClient
{
    Task<TResponse?> PostAsync<TResponse>(string url, object body, Dictionary<string, string> headers, CancellationToken ct = default);
    Task<TResponse?> GetAsync<TResponse>(string url, Dictionary<string, string>? query, Dictionary<string, string> headers, CancellationToken ct = default);
}
