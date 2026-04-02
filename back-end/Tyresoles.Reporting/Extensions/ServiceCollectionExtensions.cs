using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tyresoles.Reporting.Abstractions;
using Tyresoles.Reporting.Configuration;
using Tyresoles.Reporting.Core;

namespace Tyresoles.Reporting.Extensions;

/// <summary>
/// DI registration for Tyresoles.Reporting.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers reporting services and binds options from configuration section "Tyresoles:Reporting".
    /// When EnableResponseCache is true, ensures IMemoryCache is available (add AddMemoryCache() if not already registered).
    /// </summary>
    public static IServiceCollection AddTyresolesReporting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReportingOptions>(configuration.GetSection(ReportingOptions.SectionName));

        var enableResponseCache = configuration.GetSection(ReportingOptions.SectionName).GetValue<bool>(nameof(ReportingOptions.EnableResponseCache));
        if (enableResponseCache && !services.Any(x => x.ServiceType == typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache)))
            services.AddMemoryCache();

        services.AddSingleton<IReportRenderer, ReportViewerReportRenderer>();

        return services;
    }
}
