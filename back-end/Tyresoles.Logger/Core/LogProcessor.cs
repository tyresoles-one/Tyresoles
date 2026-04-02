
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tyresoles.Logger.Configuration;

namespace Tyresoles.Logger.Core;

public class LogProcessor : BackgroundService
{
    private readonly Channel<LogEntry> _channel;
    private readonly LogConfig _config;
    private StreamWriter? _textWriter;
    private FileStream? _textStream;
    private Utf8JsonWriter? _jsonWriter;
    private FileStream? _jsonStream;
    private JsonSerializerOptions? _jsonOptions;
    private DateTime _currentDate;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LogProcessor(LogConfig config)
    {
        _config = config;
        // DropWrite means TryWrite returns true (success) if space, or drops if full?
        // Actually DropWrite makes TryWrite return true but drops oldest? No, DropWrite drops *newest*.
        // BoundedChannelFullMode.DropNewest is the enum. DropWrite matches that behavior usually.
        // Wait, DropWrite means write operation completes synchronously but item is dropped?
        // Let's verify standard behavior.
        // BoundedChannelFullMode.DropWrite: "When the channel is full, the item being written is dropped."
        // CreateBounded(options)
        
        var opts = new BoundedChannelOptions(_config.BufferSize) 
        { 
            // When the channel is full we drop the OLDEST item so newer log entries (including errors)
            // have a higher chance to be persisted.
            FullMode = _config.DropOnFull ? BoundedChannelFullMode.DropOldest : BoundedChannelFullMode.Wait 
        };
        _channel = Channel.CreateBounded<LogEntry>(opts);
    }
    
    internal void Enqueue(LogEntry entry)
    {
        // If DropOnFull is true, TryWrite returns true (item accepted or dropped internally depending on mode).
        // If Wait, TryWrite returns false if full.
        
        if (!_channel.Writer.TryWrite(entry))
        {
            if (!_config.DropOnFull)
            {
                 // Sync blocking implementation for safety (not recommended for perf)
                 var task = _channel.Writer.WriteAsync(entry).AsTask();
                 task.Wait();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EnsureFileOpenAsync();
            
            // Batch processing for fewer IO calls
            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_channel.Reader.TryRead(out var entry))
                {
                    try 
                    {
                        await WriteLogAsync(entry);
                    }
                    catch (Exception ex)
                    {
                        // Last-resort fallback when the log pipeline itself fails (no ILogger available here).
                        Trace.WriteLine($"[Tyresoles.Logger] LOG PROCESSOR ERROR: {ex}");
                    }
                }
                
                await FlushAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        finally
        {
            await CloseFilesAsync();
        }
    }
    
    private async Task WriteLogAsync(LogEntry entry)
    {
        // Check Roll
        if (DateTime.Today != _currentDate)
        {
            await RollFilesAsync();
        }
        
        if (_config.Format == LogFormat.Text)
        {
            if (_textWriter != null)
            {
                var msg = FormatText(entry);
                await _textWriter.WriteLineAsync(msg);
            }
        }
        else if (_config.Format == LogFormat.Json)
        {
            if (_jsonWriter != null)
            {
                JsonSerializer.Serialize(_jsonWriter, entry, _jsonOptions); 
                await _jsonWriter.FlushAsync();
                await _jsonStream!.WriteAsync(new byte[] { (byte)'\n' }); 
                _jsonWriter.Reset(_jsonStream);
            }
        }
    }
    
    private string FormatText(LogEntry entry)
    {
        var isSql = entry.Category.Contains("Tyresoles.Sql", StringComparison.OrdinalIgnoreCase);
        var levelStr = GetLevelString(entry.Level);
        var prefix = isSql ? $"SQL {levelStr}" : levelStr;
        return $"{prefix} [{entry.Method}] {entry.Message}{(entry.Exception != null ? "\n" + entry.Exception.ToString() : "")}";
    }
    
    private static string GetLevelString(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "INF"
    };

    private async Task EnsureFileOpenAsync()
    {
        _currentDate = DateTime.Today;
        var baseDir = _config.LogDirectory;
        if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
        
        var dateStr = _currentDate.ToString("yyyyMMdd");
        
        // Process-specific suffix to avoid locking issues in multi-process environments
        var suffix = $"{System.Diagnostics.Process.GetCurrentProcess().Id}_{AppDomain.CurrentDomain.Id}";
        
        if (_config.Format == LogFormat.Text)
        {
            var path = Path.Combine(baseDir, $"{_config.FilePrefix}_{dateStr}_{suffix}.log");
            // FileShare.Read allows external tools (like tail) to read while we write.
            // FileMode.Append ensures we don't overwrite if restart happens.
            _textStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true);
            _textWriter = new StreamWriter(_textStream) { AutoFlush = false }; 
        }
        else if (_config.Format == LogFormat.Json)
        {
            var path = Path.Combine(baseDir, $"{_config.FilePrefix}_{dateStr}_{suffix}.json");
            _jsonStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true);
            _jsonWriter = new Utf8JsonWriter(_jsonStream, new JsonWriterOptions { Indented = false });
            _jsonOptions = new JsonSerializerOptions();
            _jsonOptions.Converters.Add(new LogEntryConverter());
        }

        // Trigger cleanup
        _ = CleanOldFilesAsync(); 
    }
    
    private async Task RollFilesAsync()
    {
        await CloseFilesAsync();
        await EnsureFileOpenAsync();
    }
    
    private async Task CloseFilesAsync()
    {
        if (_textWriter != null) { await _textWriter.DisposeAsync(); _textWriter = null; }
        if (_textStream != null) { await _textStream.DisposeAsync(); _textStream = null; }
        
        if (_jsonWriter != null) { await _jsonWriter.DisposeAsync(); _jsonWriter = null; }
        if (_jsonStream != null) { await _jsonStream.DisposeAsync(); _jsonStream = null; }
    }
    
    private async Task FlushAsync()
    {
        if (_textWriter != null) await _textWriter.FlushAsync();
        if (_jsonWriter != null) await _jsonWriter.FlushAsync();
        if (_jsonStream != null) await _jsonStream.FlushAsync();
    }
    
    private Task CleanOldFilesAsync()
    {
        return Task.Run(() => 
        {
            try
            {
                var dir = new DirectoryInfo(_config.LogDirectory);
                // Clean up any old files from any process
                var files = dir.GetFiles($"{_config.FilePrefix}*.*");
                var threshold = DateTime.Today.AddDays(-_config.MaxRetentionDays);
                
                foreach(var f in files)
                {
                    if (f.LastWriteTime < threshold) // Use LastWriteTime as robust check
                    {
                        try { f.Delete(); } catch { /* Ignore if locked by another process */ }
                    }
                }
            }
            catch { /* Ignore directory access errors */ }
        });
    }
}
