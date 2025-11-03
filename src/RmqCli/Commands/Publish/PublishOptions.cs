using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Commands.Publish;

/// <summary>
/// Options specific to the publish command.
/// </summary>
public class PublishOptions
{
    public DestinationInfo Destination { get; init; } = null!;
    public string? MessageBody { get; init; }
    public FileInfo? MessageFile { get; init; }
    public int BurstCount { get; init; }
    public bool IsStdinRedirected { get; init; }

    // Message properties from CLI flags
    public string? AppId { get; init; }
    public string? ClusterId { get; init; }
    public string? ContentType { get; init; }
    public string? ContentEncoding { get; init; }
    public string? CorrelationId { get; init; }
    public DeliveryModes? DeliveryMode { get; init; }
    public string? Expiration { get; init; }
    public byte? Priority { get; init; }
    public string? ReplyTo { get; init; }
    public string? Type { get; init; }
    public string? UserId { get; init; }
    public Dictionary<string, object>? Headers { get; init; }

    // JSON message options
    public string? JsonMessage { get; init; }
}
