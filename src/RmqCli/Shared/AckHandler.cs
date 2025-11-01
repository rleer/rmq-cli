using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Shared;

public class AckHandler
{
    private readonly ILogger<AckHandler> _logger;

    public AckHandler(ILogger<AckHandler> logger)
    {
        _logger = logger;
    }

    public async Task DispatchAcknowledgments(Channel<(ulong deliveryTag, AckModes ackMode)> ackChan, IChannel rmqChannel)
    {
        _logger.LogDebug("Starting acknowledgment dispatcher");
        await foreach (var (deliveryTag, ackModeValue) in ackChan.Reader.ReadAllAsync())
        {
            switch (ackModeValue)
            {
                case AckModes.Ack:
                    _logger.LogTrace("Acknowledging message #{DeliveryTag}", deliveryTag);
                    await rmqChannel.BasicAckAsync(deliveryTag, multiple: false);
                    break;
                case AckModes.Reject:
                    _logger.LogTrace("Rejecting message #{DeliveryTag} without requeue", deliveryTag);
                    await rmqChannel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
                    break;
                case AckModes.Requeue:
                    _logger.LogTrace("Requeue message #{DeliveryTag}", deliveryTag);
                    await rmqChannel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
                    break;
            }
        }

        _logger.LogDebug("Acknowledgement dispatcher finished");
    }
}