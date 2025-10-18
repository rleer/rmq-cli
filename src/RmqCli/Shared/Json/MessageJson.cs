using System.Text.Json.Serialization;

namespace RmqCli.Shared.Json;

public record MessageJson(
    string Exchange,
    string RoutingKey,
    string Queue,
    ulong DeliveryTag,
    bool Redelivered,
    [property: JsonConverter(typeof(BodyJsonConverter))]
    string Body,
    long BodySizeBytes,
    string BodySize,
    Dictionary<string, object>? Properties = null
);

public record MessageJsonArray(MessageJson[] Messages);
