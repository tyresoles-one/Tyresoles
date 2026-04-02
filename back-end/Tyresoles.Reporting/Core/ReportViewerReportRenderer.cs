using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Reporting.NETCore;
using Tyresoles.Reporting.Abstractions;
using Tyresoles.Reporting.Configuration;

namespace Tyresoles.Reporting.Core;

/// <summary>
/// ReportViewerCore-based implementation of IReportRenderer with optional definition and response caching.
/// </summary>
public sealed class ReportViewerReportRenderer : IReportRenderer
{
    private readonly ReportingOptions _options;
    private readonly IMemoryCache? _memoryCache;
    private readonly SemaphoreSlim? _concurrencyLimit;
    private readonly ConcurrentDictionary<string, byte[]> _definitionCache = new();
    private readonly string? _reportsPath;

    public ReportViewerReportRenderer(
        IOptions<ReportingOptions> options,
        IMemoryCache? memoryCache = null)
    {
        _options = options.Value;
        _memoryCache = _options.EnableResponseCache ? memoryCache : null;
        _reportsPath = string.IsNullOrWhiteSpace(_options.ReportsPath) ? null : _options.ReportsPath.TrimEnd('/', '\\');
        _concurrencyLimit = _options.MaxConcurrentRenders > 0
            ? new SemaphoreSlim(_options.MaxConcurrentRenders)
            : null;
    }

    public Task<Stream> RenderPdfAsync(string reportName, ReportInput input, CancellationToken cancellationToken = default)
        => RenderFormatAsync(reportName, input, "PDF", cancellationToken);

    public Task<Stream> RenderExcelAsync(string reportName, ReportInput input, CancellationToken cancellationToken = default)
        => RenderFormatAsync(reportName, input, "EXCELOPENXML", cancellationToken);

    private async Task<Stream> RenderFormatAsync(string reportName, ReportInput input, string format, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportName))
            throw new ArgumentException("Report name is required.", nameof(reportName));

        if (_concurrencyLimit != null)
            await _concurrencyLimit.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_options.EnableResponseCache && _memoryCache != null && input != null && (input.DataSources == null || input.DataSources.Count == 0))
            {
                var key = CacheKey(reportName, input, format);
                if (_memoryCache.TryGetValue(key, out Stream? cached) && cached != null)
                {
                    var copy = new MemoryStream();
                    cached.Position = 0;
                    await cached.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
                    copy.Position = 0;
                    return copy;
                }
            }

            byte[] definition = await GetReportDefinitionAsync(reportName, cancellationToken).ConfigureAwait(false);
            byte[] bytes = await Task.Run(() => RenderFormatCore(definition, input, format), cancellationToken).ConfigureAwait(false);

            if (_options.EnableResponseCache && _memoryCache != null && input != null && (input.DataSources == null || input.DataSources.Count == 0))
            {
                var key = CacheKey(reportName, input, format);
                var streamToCache = new MemoryStream(bytes, writable: false);
                _memoryCache.Set(key, streamToCache, TimeSpan.FromSeconds(_options.ResponseCacheSeconds));
            }

            return new MemoryStream(bytes, writable: false);
        }
        finally
        {
            _concurrencyLimit?.Release();
        }
    }

    private static string CacheKey(string reportName, ReportInput input, string format)
    {
        var ps = input.Parameters;
        if (ps == null || ps.Count == 0)
            return $"r:{reportName}:{format}";
        var seed = reportName + "|" + format + "|" + string.Join(";", ps.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        using var hasher = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToHexString(hasher.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed)));
        return $"r:{reportName}:{format}:" + hash;
    }

    private async Task<byte[]> GetReportDefinitionAsync(string reportName, CancellationToken cancellationToken)
    {
        if (_options.EnableDefinitionCache && _definitionCache.TryGetValue(reportName, out byte[]? cached))
            return cached;

        byte[] definition;
        if (_reportsPath != null)
        {
            var path = Path.Combine(_reportsPath, reportName + ".rdlc");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Report definition not found: {path}");
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            definition = ms.ToArray();
        }
        else
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = asm.GetName().Name + ".Reports." + reportName + ".rdlc";
            await using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded report not found: {resourceName}");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            definition = ms.ToArray();
        }

        if (_options.EnableDefinitionCache)
            _definitionCache.TryAdd(reportName, definition);

        return definition;
    }

    private static byte[] RenderFormatCore(byte[] definition, ReportInput? input, string format)
    {
        using var report = new LocalReport();
        report.LoadReportDefinition(new MemoryStream(definition));

        if (input?.Parameters != null && input.Parameters.Count > 0)
        {
            var definedParameters = report.GetParameters().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var parameters = input.Parameters
                .Where(p => definedParameters.Contains(p.Key))
                .Select(p => new ReportParameter(p.Key, p.Value?.ToString() ?? string.Empty))
                .ToArray();
            
            if (parameters.Length > 0)
            {
                report.SetParameters(parameters);
            }
        }

        if (input?.DataSources != null)
        {
            foreach (var kv in input.DataSources)
                report.DataSources.Add(new ReportDataSource(kv.Key, kv.Value));
        }

        return report.Render(format);
    }
}
