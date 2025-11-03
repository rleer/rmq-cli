using System.Text.Json;
using RmqCli.Core.Models;
using RmqCli.Shared.Json;

namespace RmqCli.Commands.Publish;

/// <summary>
/// Parses JSON messages for publishing using source-generated serialization.
/// </summary>
public static class JsonMessageParser
{
    /// <summary>
    /// Parses a single JSON message using source-generated serialization.
    /// </summary>
    public static Message ParseSingle(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize(
                json,
                JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(Message)));

            if (message == null)
            {
                throw new ArgumentException("Failed to parse JSON message: result was null");
            }

            var result = (Message)message;

            // Convert JsonElement header values to actual types for RabbitMQ serialization
            if (result.Headers != null)
            {
                var normalizedHeaders = NormalizeHeaderValues(result.Headers);
                result = result with
                {
                    Headers = normalizedHeaders
                };
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON message format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts JsonElement values in a dictionary to their actual types (string, long, double, bool).
    /// RabbitMQ cannot serialize JsonElement objects, so we need to convert them to primitive types.
    /// </summary>
    private static Dictionary<string, object> NormalizeHeaderValues(Dictionary<string, object> headers)
    {
        var normalized = new Dictionary<string, object>();

        foreach (var (key, value) in headers)
        {
            normalized[key] = value switch
            {
                JsonElement jsonElement => ConvertJsonElement(jsonElement),
                _ => value
            };
        }

        return normalized;
    }

    /// <summary>
    /// Converts a JsonElement to its actual type.
    /// </summary>
    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var longVal) ? longVal : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => string.Empty,
            _ => element.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Parses newline-delimited JSON (NDJSON) messages.
    /// </summary>
    public static List<Message> ParseNdjson(string ndjson)
    {
        var messages = new List<Message>();
        var lines = ndjson.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var message = ParseSingle(line);
                messages.Add(message);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Failed to parse line {i + 1}: {ex.Message}", ex);
            }
        }

        return messages;
    }
}
