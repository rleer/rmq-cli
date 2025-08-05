using System.Text.Json;
using System.Text.Json.Serialization;

namespace RmqCli.ConsumeCommand.MessageFormatter.Json;

/// <summary>
/// Converts the message body string so that valid JSON is emitted as raw JSON and
/// invalid JSON is emitted as a string with relaxed escaping.
/// </summary>
public class BodyJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString();

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value))
        {
            writer.WriteStringValue(value);
            return;
        }

        // Trim whitespace and check if the text looks like JSON
        var trimmed = value.Trim();
        var looksLikeJson = (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                            (trimmed.StartsWith("[") && trimmed.EndsWith("]"));

        if (looksLikeJson)
        {
            // Try to parse the body as JSON.  If parsing succeeds, write it as raw JSON.
            try
            {
                using var doc = JsonDocument.Parse(value);
                doc.RootElement.WriteTo(writer);
                return;
            }
            catch (JsonException)
            {
                // fall through and write as string
            }
        }

        // If not valid JSON, write as a normal string (encoder is set to relaxed mode via options).
        writer.WriteStringValue(value);
    }
}