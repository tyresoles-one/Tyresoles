
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tyresoles.Logger.Core;

// JSON Converter for ScopeNode chain flattening
public class LogEntryConverter : JsonConverter<LogEntry>
{
    public override LogEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException("Deserialization not supported");
    }

    public override void Write(Utf8JsonWriter writer, LogEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Preserve the real log severity even for SQL entries (so "Error" doesn't look like "SQL").
        var isSql = value.Category.Contains("Tyresoles.Sql", StringComparison.OrdinalIgnoreCase);
        var levelStr = isSql ? $"SQL/{value.Level}" : value.Level.ToString();
        writer.WriteString("level", levelStr);
        writer.WriteString("method", value.Method);
        writer.WriteString("message", value.Message);
        
        if (value.Exception != null)
        {
            writer.WriteString("exception", value.Exception.ToString());
        }
        
        writer.WriteEndObject();
    }
}
