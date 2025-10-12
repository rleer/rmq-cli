using RabbitMQ.Client;

namespace RmqCli.ConsumeCommand.MessageFormatter;

/// <summary>
/// Represents extracted message properties from RabbitMQ.
/// All values are pre-converted to appropriate types for easy formatting.
/// </summary>
public class FormattedMessageProperties
{
    public string? Type { get; init; }
    public string? MessageId { get; init; }
    public string? AppId { get; init; }
    public string? ClusterId { get; init; }
    public string? ContentType { get; init; }
    public string? ContentEncoding { get; init; }
    public string? CorrelationId { get; init; }
    public DeliveryModes? DeliveryMode { get; init; }
    public string? Expiration { get; init; }
    public byte? Priority { get; init; }
    public string? ReplyTo { get; init; }
    public string? Timestamp { get; init; }
    public Dictionary<string, object>? Headers { get; init; }

    /// <summary>
    /// Returns true if any property is present (not null).
    /// </summary>
    public bool HasAnyProperty()
    {
        return Type != null || MessageId != null || AppId != null || ClusterId != null ||
               ContentType != null || ContentEncoding != null || CorrelationId != null ||
               DeliveryMode != null || Expiration != null || Priority != null ||
               ReplyTo != null || Timestamp != null || Headers != null;
    }
}
