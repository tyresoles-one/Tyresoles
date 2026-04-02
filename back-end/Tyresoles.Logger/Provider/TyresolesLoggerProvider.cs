
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Logger.Configuration;
using Tyresoles.Logger.Core;

namespace Tyresoles.Logger.Provider;

[ProviderAlias("Tyresoles")]
public class TyresolesLoggerProvider : ILoggerProvider, IDisposable
{
    private readonly LogConfig _config;
    private readonly LogProcessor _processor;
    private readonly ConcurrentDictionary<string, TyresolesLogger> _loggers = new();
    private bool _disposed;

    public TyresolesLoggerProvider(IOptions<LogConfig> config)
    {
        _config = config.Value;
        _processor = new LogProcessor(_config);
        // We start the background loop. We don't await because it runs forever.
        _processor.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new TyresolesLogger(name, _processor, _config));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _processor.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _processor.Dispose();
        _loggers.Clear();
    }
}
