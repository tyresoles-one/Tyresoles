
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Tyresoles.Logger.Configuration;
using Tyresoles.Logger.Provider;

namespace Tyresoles.Logger.Extensions;

public static class LoggingExtensions
{
    public static ILoggingBuilder AddTyresolesLogger(this ILoggingBuilder builder, Action<LogConfig>? configure = null)
    {
        builder.Services.AddSingleton<ILoggerProvider, TyresolesLoggerProvider>();
        
        if (configure != null)
        {
            builder.Services.Configure<LogConfig>(configure);
        }
        
        return builder;
    }

    public static ILoggingBuilder AddTyresolesLogger(this ILoggingBuilder builder, IConfiguration configuration)
    {
        builder.Services.AddSingleton<ILoggerProvider, TyresolesLoggerProvider>();
        builder.Services.Configure<LogConfig>(configuration);
        return builder;
    }
}
