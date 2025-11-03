namespace RmqCli.Core.Models;

/// <summary>
/// Represents a RabbitMQ message with body, properties, and headers.
/// Used for publishing messages (JSON input and internal representation).
/// </summary>
public record Message
{
    public string Body { get; init; } = string.Empty;
    public MessageProperties? Properties { get; init; }
    public Dictionary<string, object>? Headers { get; init; }
}
