using RabbitMQ.Client;
using RmqCli.Shared;

namespace RmqCli.Commands.Publish;

public record PublishOperationDto(
    string MessageId,
    long MessageLength,
    AmqpTimestamp AmqTime)
{
    public string MessageSize => OutputUtilities.ToSizeString(MessageLength);
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeSeconds(AmqTime.UnixTime);
}