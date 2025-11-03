using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Output.Formatters;
using RmqCli.Shared;

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
        Channel<RetrievedMessage> receiveChan,
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

            // Extract properties and headers
            var body = Encoding.UTF8.GetString(result.Body.ToArray());
            var (properties, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(result.BasicProperties);
            var bodySizeBytes = Encoding.UTF8.GetByteCount(body);

            var message = new RetrievedMessage
            {
                Body = body,
                Properties = properties,
                Headers = headers,
                Exchange = result.Exchange,
                RoutingKey = result.RoutingKey,
                Queue = queue,
                DeliveryTag = result.DeliveryTag,
                Redelivered = result.Redelivered,
                BodySizeBytes = bodySizeBytes,
                BodySize = OutputUtilities.ToSizeString(bodySizeBytes)
            };

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