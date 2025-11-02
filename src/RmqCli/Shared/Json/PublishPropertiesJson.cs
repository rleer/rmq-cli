using RabbitMQ.Client;

namespace RmqCli.Shared.Json;

/// <summary>
/// Represents RabbitMQ message properties for publishing.
/// Uses camelCase naming (configured in JsonSerializationContext).
/// </summary>
public class PublishPropertiesJson
{
    public string? AppId { get; set; }
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? CorrelationId { get; set; }
    public DeliveryModes? DeliveryMode { get; set; }
    public string? Expiration { get; set; }
    public string? MessageId { get; set; }
    public byte? Priority { get; set; }
    public string? ReplyTo { get; set; }
    public long? Timestamp { get; set; }
    public string? Type { get; set; }
    public string? UserId { get; set; }
}
