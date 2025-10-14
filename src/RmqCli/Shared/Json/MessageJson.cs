using System.Text.Json.Serialization;

namespace RmqCli.Shared.Json;

public record MessageJson(
    ulong DeliveryTag,
    bool Redelivered,
    [property: JsonConverter(typeof(BodyJsonConverter))]
    string Body,
    Dictionary<string, object>? Properties = null
);

public record MessageJsonArray(MessageJson[] Messages);
