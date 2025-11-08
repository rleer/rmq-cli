using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Commands.Publish;
using RmqCli.Core.Models;

namespace RmqCli.Shared.Json;

[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(Message[]))]
[JsonSerializable(typeof(RetrievedMessage))]
[JsonSerializable(typeof(RetrievedMessage[]))]
[JsonSerializable(typeof(MessageProperties))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(RabbitMQ.Client.DeliveryModes))]
[JsonSerializable(typeof(Response))]
[JsonSerializable(typeof(PublishResponse))]
[JsonSerializable(typeof(PublishResult))]
[JsonSerializable(typeof(MessageRetrievalResponse))]
[JsonSerializable(typeof(MessageRetrievalResult))]
[JsonSerializable(typeof(ErrorInfo))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
)]
public partial class JsonSerializationContext : JsonSerializerContext
{
    private static JsonSerializationContext? _relaxedEscaping;

    // Singleton instance with relaxed escaping for JSON serialization.
    public static JsonSerializationContext RelaxedEscaping => _relaxedEscaping ??= new JsonSerializationContext(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = Default
    });
}