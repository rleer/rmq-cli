using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RmqCli.MessageFormatter.Json;

[JsonSerializable(typeof(MessageJson))]
[JsonSerializable(typeof(MessageJson[]))]
[JsonSerializable(typeof(MessageJsonArray))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(RabbitMQ.Client.DeliveryModes))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
)]
public partial class JsonSerializationContext : JsonSerializerContext
{
    // Declare new JsonSerializerOptions with relaxed escaping for JSON serialization.
    public static JsonSerializerOptions RelaxedEscapingOptions => new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = Default
    };
}