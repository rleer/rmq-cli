using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rmq;

/// <summary>
/// Reading and writing the NDJSON line described in docs/message-schema.md.
/// Source-generated throughout — no reflection, no runtime code generation.
/// </summary>
public static class MessageJson
{
    public static string Serialize(Message message) =>
        JsonSerializer.Serialize(message, MessageJsonContext.Relaxed.Message);

    public static void Write(Utf8JsonWriter writer, Message message) =>
        JsonSerializer.Serialize(writer, message, MessageJsonContext.Relaxed.Message);

    /// <summary>Parses one NDJSON line.</summary>
    /// <exception cref="ArgumentException">The line is not valid JSON, or not a valid message.</exception>
    public static Message Parse(string line)
    {
        Message? message;
        try
        {
            message = JsonSerializer.Deserialize(line, MessageJsonContext.Relaxed.Message);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON message: {ex.Message}", ex);
        }

        if (message == null)
        {
            throw new ArgumentException("Invalid JSON message: expected an object, got null");
        }

        if (message.BodyEncoding is not (null or Message.Base64Encoding))
        {
            throw new ArgumentException($"Unsupported bodyEncoding '{message.BodyEncoding}': expected \"{Message.Base64Encoding}\" or nothing");
        }

        if (message.BodyEncoding == Message.Base64Encoding && !IsBase64(message.Body))
        {
            throw new ArgumentException("bodyEncoding is \"base64\" but body is not valid base64");
        }

        // Header values arrive as JsonElement; RabbitMQ cannot serialize those.
        if (message.Properties?.Headers is { Count: > 0 } headers)
        {
            message = message with
            {
                Properties = message.Properties with { Headers = NormalizeHeaders(headers) }
            };
        }

        return message;
    }

    /// <summary>Reads NDJSON a line at a time, so a consume pipe streams rather than buffering.</summary>
    public static async IAsyncEnumerable<Message> ReadLinesAsync(TextReader reader, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var lineNumber = 0;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Message message;
            try
            {
                message = Parse(line);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Line {lineNumber}: {ex.Message}", ex);
            }

            yield return message;
        }
    }

    private static bool IsBase64(string value) =>
        Convert.TryFromBase64String(value, new byte[((value.Length * 3) >> 2) + 4], out _);

    private static Dictionary<string, object> NormalizeHeaders(Dictionary<string, object> headers)
    {
        var normalized = new Dictionary<string, object>(headers.Count);
        foreach (var (key, value) in headers)
        {
            normalized[key] = value is JsonElement element ? ConvertJsonElement(element) : value;
        }

        return normalized;
    }

    /// <summary>
    /// Back to the same closed set Amqp.ToHeaderValue produces, nested shapes included —
    /// otherwise a consumed x-death header would republish as a JSON string rather than
    /// the field table it was, and the properties round-trip would not hold.
    /// </summary>
    private static object ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;
            case JsonValueKind.Number:
                return element.TryGetInt64(out var integer) ? integer : element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Object:
                var table = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    table[property.Name] = ConvertJsonElement(property.Value);
                }

                return table;
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }

                return list;
            default:
                return string.Empty;
        }
    }
}

/// <summary>
/// Emits a JSON body inline when it is JSON, and reads back whatever it wrote.
/// The old converter wrote raw JSON but read with GetString(), which threw on the
/// object it had just written — that asymmetry broke `consume | publish`.
/// </summary>
public sealed class BodyConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? string.Empty;
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return string.Empty;
        }

        // Object, array, number, or boolean: the body is the compact text of that value.
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(value))
        {
            writer.WriteStringValue(value);
            return;
        }

        var trimmed = value.AsSpan().Trim();
        var looksLikeJson = (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                            (trimmed.StartsWith("[") && trimmed.EndsWith("]"));

        if (looksLikeJson)
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                document.RootElement.WriteTo(writer);
                return;
            }
            catch (JsonException)
            {
                // Looked like JSON, wasn't. Fall through and write it as a string.
            }
        }

        writer.WriteStringValue(value);
    }
}

[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(MessageProperties))]
[JsonSerializable(typeof(Dictionary<string, object>))]
// A dead-lettered message's x-death header is a list of nested field tables, so both
// shapes have to be writable. byte[] stays unregistered on purpose — see Amqp.ToHeaderValue.
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(RabbitMQ.Client.DeliveryModes))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(object))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
public partial class MessageJsonContext : JsonSerializerContext
{
    private static MessageJsonContext? _relaxed;

    /// <summary>
    /// Relaxed escaping keeps non-ASCII bodies readable instead of \uXXXX-escaping them.
    /// NDJSON is a text interchange format; escaping every umlaut helps nobody.
    /// </summary>
    public static MessageJsonContext Relaxed => _relaxed ??= new MessageJsonContext(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        TypeInfoResolver = Default
    });
}
