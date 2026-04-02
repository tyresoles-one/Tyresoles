using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tyresoles.Data.Features.Sales.Reports;

/// <summary>
/// Deserializes <c>customers</c> from either a comma-separated string or a JSON array of strings (REST compatibility).
/// </summary>
public sealed class CustomersFilterJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.StartArray => ReadArray(ref reader),
            _ => throw new JsonException("Unexpected JSON for customers filter."),
        };
    }

    private static string? ReadArray(ref Utf8JsonReader reader)
    {
        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s.Trim());
            }
        }

        if (list.Count == 0)
            return null;
        return string.Join(",", list);
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}
