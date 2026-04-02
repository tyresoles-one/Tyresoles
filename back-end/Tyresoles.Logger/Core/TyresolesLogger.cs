
using Microsoft.Extensions.Logging;
using Tyresoles.Logger.Configuration;

namespace Tyresoles.Logger.Core;

public class TyresolesLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogProcessor _processor;
    private readonly LogConfig _config;
    
    // Static AsyncLocal for context propagation
    internal static AsyncLocal<ScopeNode?> CurrentScope = new();

    public TyresolesLogger(string categoryName, LogProcessor processor, LogConfig config)
    {
        _categoryName = categoryName;
        _processor = processor;
        _config = config;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var parent = CurrentScope.Value;
        var node = new ScopeNode(state!, parent); 
        CurrentScope.Value = node;
        return new ScopeDisposable(parent);
    }
    
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _config.MinLevel;
    
    private string GetCallingMethod()
    {
        try
        {
            // Skip frames inside Tyresoles.Logger and Microsoft.Logging
            var stack = new System.Diagnostics.StackTrace();
            for (int i = 2; i < stack.FrameCount; i++)
            {
                var method = stack.GetFrame(i)?.GetMethod();
                if (method == null || method.DeclaringType == null) continue;

                var type = method.DeclaringType;
                var typeFullName = type.FullName ?? "";
                if (typeFullName.StartsWith("Tyresoles.Logger") || 
                    typeFullName.StartsWith("Tyresoles.Sql") ||
                    typeFullName.StartsWith("Microsoft.Extensions.Logging") || 
                    typeFullName.StartsWith("System.Runtime") ||
                    typeFullName.StartsWith("System.Reflection") ||
                    typeFullName.StartsWith("System.Diagnostics")) continue;
                
                var className = type.Name;
                var methodName = method.Name;

                // Handle async state machine
                if (methodName == "MoveNext" && className.Contains("<") && className.Contains(">"))
                {
                    var start = className.IndexOf('<');
                    var end = className.IndexOf('>');
                    if (start >= 0 && end > start)
                    {
                        methodName = className.Substring(start + 1, end - start - 1);
                        className = type.DeclaringType?.Name ?? className;
                    }
                }

                return $"{className}.{methodName}";
            }
        }
        catch { }
        return "Unknown";
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        
        var entry = new LogEntry(
            DateTime.UtcNow, 
            logLevel, 
            _categoryName, 
            eventId, 
            message, 
            exception,
            GetCallingMethod(),
            CurrentScope.Value 
        );
        
        _processor.Enqueue(entry);
    }
    
    private sealed class ScopeDisposable : IDisposable
    {
        private readonly ScopeNode? _parent;
        
        public ScopeDisposable(ScopeNode? parent) => _parent = parent;
        
        public void Dispose() 
        {
            CurrentScope.Value = _parent;
        }
    }
}
