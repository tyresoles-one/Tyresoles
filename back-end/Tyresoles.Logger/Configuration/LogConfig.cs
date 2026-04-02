using Microsoft.Extensions.Logging;

namespace Tyresoles.Logger.Configuration;

public class LogConfig
{
    public string LogDirectory { get; set; } = "logs";
    public string FilePrefix { get; set; } = "tyre";
    public int MaxRetentionDays { get; set; } = 3;
    public LogFormat Format { get; set; } = LogFormat.Json;
    public int BufferSize { get; set; } = 10000;
    public bool DropOnFull { get; set; } = true;
    public LogLevel MinLevel { get; set; } = LogLevel.Information;
}

public enum LogFormat { Text, Json }
