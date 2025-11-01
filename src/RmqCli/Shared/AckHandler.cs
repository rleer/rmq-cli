using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Core.Models;

namespace RmqCli.Shared;

public class AckHandler
{
    private readonly ILogger<AckHandler> _logger;
    private readonly MessageRetrievalOptions _options;

    private const ulong MaxBatchSize = 100;

    public AckHandler(ILogger<AckHandler> logger, MessageRetrievalOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public async Task DispatchAcknowledgments(Channel<(ulong deliveryTag, bool success)> ackChan, IChannel rmqChannel)
    {
        _logger.LogDebug("Starting acknowledgment dispatcher");

        // In Requeue mode, we do not perform any acknowledgments here
        if (_options.AckMode is AckModes.Requeue)
        {
            _logger.LogDebug("Acknowledgment dispatcher exiting early due to requeue mode");
            return;
        }

        ulong lastDeliveryTag = 0;
        ulong lastReportedDeliveryTag = 0;
        var batchSize = _options.PrefetchCount <= 0 ? MaxBatchSize : _options.PrefetchCount;

        await foreach (var (deliveryTag, success) in ackChan.Reader.ReadAllAsync())
        {
            if (success && lastDeliveryTag - lastReportedDeliveryTag >= batchSize)
            {
                await PerformAckAsync(rmqChannel, lastDeliveryTag, multiple: true);
                lastReportedDeliveryTag = lastDeliveryTag;
            }
            else if (!success)
            {
                _logger.LogTrace("Requeue message #{DeliveryTag}", deliveryTag);
                await rmqChannel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
                break;
            }
            lastDeliveryTag = deliveryTag;
        }

        if (lastReportedDeliveryTag < lastDeliveryTag)
        {
            _logger.LogTrace("Final batch acknowledgment up to message #{DeliveryTag}", lastDeliveryTag);
            await PerformAckAsync(rmqChannel, lastDeliveryTag, multiple: true);
        }

        _logger.LogDebug("Acknowledgement dispatcher finished");
    }

    private async Task PerformAckAsync(IChannel rmqChannel, ulong deliveryTag, bool multiple = true)
    {
        switch (_options.AckMode)
        {
            case AckModes.Ack:
                _logger.LogTrace("Acknowledging message #{DeliveryTag}{BatchAck}", deliveryTag, multiple ? " (batched)" : string.Empty);
                await rmqChannel.BasicAckAsync(deliveryTag, multiple: multiple);
                break;
            case AckModes.Reject:
                _logger.LogTrace("Rejecting message #{DeliveryTag} without requeue{BatchAck}", deliveryTag, multiple ? " (batched)" : string.Empty);
                await rmqChannel.BasicNackAsync(deliveryTag, multiple: multiple, requeue: false);
                break;
        }
    }
}