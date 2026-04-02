using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tyresoles.Protean.Encryption;
using Tyresoles.Protean.Http;
using Tyresoles.Protean.Services;
using Tyresoles.Protean.Session;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Net.Http;

namespace Tyresoles.Protean;

/// <summary>
/// Extension methods for registering all Protean GSP services into the DI container.
/// Configuration is read from <see cref="Constants"/> (hardcoded); set <see cref="Constants.Environment"/> for Sandbox vs Production.
/// </summary>
public static class ProteanServiceCollectionExtensions
{
    /// <summary>
    /// Register Tyresoles.Protean services. Uses <see cref="Constants"/> for config (no appsettings).
    /// To use sandbox, set <c>Constants.Environment = ProteanEnvironment.Sandbox</c> before or at startup.
    /// </summary>
    public static IServiceCollection AddProtean(this IServiceCollection services)
    {
        // Memory cache (idempotent if already added)
        services.AddMemoryCache();

        // Encryption (singleton – fully stateless and thread-safe)
        services.TryAddSingleton<IProteanEncryptor, ProteanEncryptor>();

        // Typed HTTP clients via IHttpClientFactory
        services.AddHttpClient<IProteanHttpClient, ProteanHttpClient>((_, http) =>
        {
            http.BaseAddress = new Uri(EInvoiceUrls.Base(Constants.Sandbox));
            http.Timeout     = TimeSpan.FromSeconds(60);
        })
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IProteanEWbHttpClient, ProteanEWbHttpClient>((_, http) =>
        {
            http.BaseAddress = new Uri(EWaybillUrls.Base(Constants.Sandbox));
            http.Timeout     = TimeSpan.FromSeconds(60);
        })
        .AddPolicyHandler(GetRetryPolicy());

        // Session management (scoped per-request)
        services.TryAddScoped<IProteanSessionService, ProteanSessionService>();

        // Business services
        services.TryAddScoped<IEInvoiceService, EInvoiceService>();
        services.TryAddScoped<IEWaybillService, EWaybillService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
