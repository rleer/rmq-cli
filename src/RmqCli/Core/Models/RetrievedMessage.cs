using System.Text.Json.Serialization;
using RmqCli.Shared.Json;

namespace RmqCli.Core.Models;

/// <summary>
/// Represents a RabbitMQ message retrieved from a queue.
/// Used for consume and peek operations (output).
/// Includes routing metadata and body size information.
/// Body uses BodyJsonConverter for smart JSON serialization.
/// </summary>
public record RetrievedMessage
{
    [JsonConverter(typeof(BodyJsonConverter))]
    public string Body { get; init; } = string.Empty;
    public MessageProperties? Properties { get; init; }
    public Dictionary<string, object>? Headers { get; init; }

    // Routing metadata
    public string Exchange { get; init; } = string.Empty;
    public string RoutingKey { get; init; } = string.Empty;
    public string Queue { get; init; } = string.Empty;
    public ulong DeliveryTag { get; init; }
    public bool Redelivered { get; init; }

    // Body size information
    public long BodySizeBytes { get; init; }
    public string BodySize { get; init; } = string.Empty;
}
