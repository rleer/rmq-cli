using RabbitMQ.Client;
using RmqCli.Common;

namespace RmqCli.PublishCommand;

public record PublishOperationDto(
    string MessageId,
    long MessageLength,
    AmqpTimestamp AmqTime)
{
    public string MessageSize => OutputUtilities.ToSizeString(MessageLength);
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeSeconds(AmqTime.UnixTime);
}