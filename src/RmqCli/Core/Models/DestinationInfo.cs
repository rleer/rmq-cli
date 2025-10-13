using System.Text.Json.Serialization;

namespace RmqCli.Core.Models;

/// <summary>
/// Represents a destination for messages in a message broker system.
/// A destination can be defined either by a Queue name or by a RoutingKey.
/// </summary>
public class DestinationInfo
{
    /// <summary>
    /// Either "queue" or "exchange".
    /// </summary>
    [JsonIgnore]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the queue name.
    /// When specified, messages will be sent directly to this queue.
    /// Either Queue or RoutingKey must be provided, but not both.
    /// </summary>
    [JsonPropertyName("queue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Queue { get; set; }

    /// <summary>
    /// Gets or sets the routing key for the message.
    /// When specified, messages will be routed to queues based on this key.
    /// Either RoutingKey or Queue must be provided, but not both.
    /// </summary>
    [JsonPropertyName("routing_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RoutingKey { get; set; }

    /// <summary>
    /// Gets or sets the exchange name.
    /// Optional. If not provided, the default exchange "amq.direct" will be used.
    /// Only applicable when using RoutingKey.
    /// </summary>
    [JsonPropertyName("exchange")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Exchange { get; set; }
}
