using RabbitMQ.Client;

namespace RmqCli.Core.Models;

/// <summary>
/// Represents RabbitMQ message properties.
/// Unified model used across publish, consume, and peek commands.
/// Timestamp is stored as Unix seconds (long) for consistency with RabbitMQ.
/// </summary>
public record MessageProperties
{
    public string? AppId { get; init; }
    public string? ClusterId { get; init; }
    public string? ContentType { get; init; }
    public string? ContentEncoding { get; init; }
    public string? CorrelationId { get; init; }
    public DeliveryModes? DeliveryMode { get; init; }
    public string? Expiration { get; init; }
    public string? MessageId { get; init; }
    public byte? Priority { get; init; }
    public string? ReplyTo { get; init; }
    public long? Timestamp { get; init; } // Unix seconds
    public string? Type { get; init; }
    public string? UserId { get; init; }

    /// <summary>
    /// Returns true if any property is present (not null).
    /// </summary>
    public bool HasAnyProperty()
    {
        return Type != null || MessageId != null || AppId != null || ClusterId != null ||
               ContentType != null || ContentEncoding != null || CorrelationId != null ||
               DeliveryMode != null || Expiration != null || Priority != null ||
               ReplyTo != null || Timestamp != null || UserId != null;
    }
}