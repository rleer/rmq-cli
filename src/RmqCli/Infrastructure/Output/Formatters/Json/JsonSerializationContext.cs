using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmqCli.Commands.Consume;
using RmqCli.Commands.Publish;
using RmqCli.Core.Models;

namespace RmqCli.Infrastructure.Output.Formatters.Json;

[JsonSerializable(typeof(MessageJson))]
[JsonSerializable(typeof(MessageJson[]), TypeInfoPropertyName = "ArrayOfMessageJson")]
[JsonSerializable(typeof(MessageJsonArray))]
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
[JsonSerializable(typeof(ConsumeResponse))]
[JsonSerializable(typeof(ConsumeResult))]
[JsonSerializable(typeof(ErrorInfo))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
)]
public partial class JsonSerializationContext : JsonSerializerContext
{
    // TODO: Make indentation configurable.
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