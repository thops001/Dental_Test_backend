using System.Text.Json;
using System.Text.Json.Serialization;

namespace dental_backend.Dentally;

/// <summary>
/// Dentally returns resource ids as JSON numbers, but they are conceptually opaque strings.
/// This wrapper deserialises a number or a string transparently into a string value.
/// </summary>
[JsonConverter(typeof(JsonIdConverter))]
public readonly record struct JsonId(string Value)
{
    public override string ToString() => Value;
    public bool HasValue => !string.IsNullOrEmpty(Value);
}

public sealed class JsonIdConverter : JsonConverter<JsonId>
{
    public override JsonId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => new JsonId(reader.GetInt64().ToString()),
            JsonTokenType.String => new JsonId(reader.GetString() ?? string.Empty),
            JsonTokenType.Null => new JsonId(string.Empty),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for id.")
        };

    public override void Write(Utf8JsonWriter writer, JsonId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
