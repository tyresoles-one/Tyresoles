using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tyresoles.Protean.Http;

/// <summary>
/// A System.Text.Json converter that allows reading JSON number values as strings.
/// Useful for Protean APIs that sometimes return numeric data for string properties.
/// </summary>
internal sealed class LongToStringConverter : JsonConverter<string?>
{
    public override bool CanConvert(Type t) => t == typeof(string);

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Number
            ? reader.GetInt64().ToString()
            : reader.GetString();

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
