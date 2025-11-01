using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Commands.MessageRetrieval.Strategies;

public class PollingStrategy : IMessageRetrievalStrategy
{
    private readonly ILogger<PollingStrategy> _logger;

    public PollingStrategy(ILogger<PollingStrategy> logger)
    {
        _logger = logger;
    }

    public async Task RetrieveMessagesAsync(
        IChannel channel,
        string queue,
        Channel<RabbitMessage> receiveChan,
        int messageCount,
        ReceivedMessageCounter counter,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check if we've reached the message limit
            if (counter.Value >= messageCount)
                break;

            var result = await channel.BasicGetAsync(queue, autoAck: false, cancellationToken);

            if (result == null)
            {
                _logger.LogDebug("No more messages available in queue");
                break;
            }

            _logger.LogTrace("Peeked message #{DeliveryTag}", result.DeliveryTag);

            var message = new RabbitMessage(
                result.Exchange,
                result.RoutingKey,
                queue,
                System.Text.Encoding.UTF8.GetString(result.Body.ToArray()),
                result.DeliveryTag,
                result.BasicProperties,
                result.Redelivered
            );

            await receiveChan.Writer.WriteAsync(message, CancellationToken.None);
            counter.Increment();
            // await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        
        // Complete the channels
        receiveChan.Writer.TryComplete();
        _logger.LogDebug("Receive channel completed after peeking {Count} messages", counter.Value); 
    }

    public string StrategyName => "Polling";
}