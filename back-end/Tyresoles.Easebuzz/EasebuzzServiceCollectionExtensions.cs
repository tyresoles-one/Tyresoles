using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Tyresoles.Easebuzz.Hash;
using Tyresoles.Easebuzz.Http;
using Tyresoles.Easebuzz.Services;

namespace Tyresoles.Easebuzz;

/// <summary>
/// Extension methods for registering Tyresoles.Easebuzz services in the DI container.
/// </summary>
public static class EasebuzzServiceCollectionExtensions
{
    /// <summary>
    /// Registers Easebuzz payment gateway services. Binds configuration from "Easebuzz" section.
    /// Key and Salt must be set (from Easebuzz dashboard); validation runs at startup.
    /// </summary>
    /// <example>
    /// In Program.cs: builder.Services.AddEasebuzz();
    /// Or: builder.Services.AddEasebuzz(o => o.Sandbox = true);
    /// </example>
    public static IServiceCollection AddEasebuzz(
        this IServiceCollection services,
        Action<EasebuzzOptions>? configure = null)
    {
        services
            .AddOptions<EasebuzzOptions>()
            .BindConfiguration(EasebuzzOptions.Section)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Key) && !string.IsNullOrWhiteSpace(o.Salt),
                "Easebuzz Key and Salt must be set. Obtain them from the Easebuzz dashboard.")
            .ValidateOnStart();

        if (configure is not null)
            services.PostConfigure(configure);

        services.TryAddSingleton<IEasebuzzHashProvider, EasebuzzHashProvider>();

        services.AddHttpClient<IEasebuzzPaymentClient, EasebuzzPaymentClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<EasebuzzOptions>>().Value;
            var baseUrl = !string.IsNullOrWhiteSpace(o.BaseUrl)
                ? o.BaseUrl.TrimEnd('/') + "/"
                : EasebuzzUrls.Base(o.Sandbox);
            http.BaseAddress = new Uri(baseUrl);
            http.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 10, 120));
        });

        services.TryAddScoped<IEasebuzzPaymentService, EasebuzzPaymentService>();

        return services;
    }
}
