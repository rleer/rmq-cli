using System.Text.Json;
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
    public static PublishMessageJson ParseSingle(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize(
                json,
                JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(PublishMessageJson)));

            if (message == null)
            {
                throw new ArgumentException("Failed to parse JSON message: result was null");
            }

            return (PublishMessageJson)message;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON message format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses newline-delimited JSON (NDJSON) messages.
    /// </summary>
    public static List<PublishMessageJson> ParseNdjson(string ndjson)
    {
        var messages = new List<PublishMessageJson>();
        var lines = ndjson.Split('\n', StringSplitOptions.RemoveEmptyEntries);

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
