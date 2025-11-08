using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RmqCli.Core.Models;
using RmqCli.Shared;
using RmqCli.Shared.Output;
using RmqCli.Shared.Output.Formatters;

namespace RmqCli.Commands.MessageRetrieval.Strategies;

public class SubscriberStrategy : IMessageRetrievalStrategy
{
    private readonly ILogger<SubscriberStrategy> _logger;

    public SubscriberStrategy(ILogger<SubscriberStrategy> logger)
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
        var (messageLimitCts, combinedCts) = CreateCombinedCancellationToken(cancellationToken);
        var consumer = CreateConsumer(channel, receiveChan, queue, messageCount, counter, messageLimitCts, combinedCts.Token);

        // Start consumer
        var consumerTag = await channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer, cancellationToken);
        RegisterCancellationHandler(combinedCts.Token, messageLimitCts.Token, cancellationToken, channel, consumerTag, receiveChan);
    }

    public string StrategyName => "Subscribe";

    private (CancellationTokenSource MessageLimitCts, CancellationTokenSource CombinedCts) CreateCombinedCancellationToken(
        CancellationToken userCancellationToken)
    {
        var messageLimitCts = new CancellationTokenSource();
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(userCancellationToken, messageLimitCts.Token);
        return (messageLimitCts, combinedCts);
    }

    private AsyncEventingBasicConsumer CreateConsumer(
        IChannel channel,
        Channel<RetrievedMessage> receiveChan,
        string queue,
        int messageCount,
        ReceivedMessageCounter counter,
        CancellationTokenSource messageLimitCts,
        CancellationToken combinedCancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += OnConsumerOnReceivedAsync;

        return consumer;

        // Event handler for received messages
        async Task OnConsumerOnReceivedAsync(object _, BasicDeliverEventArgs ea)
        {
            // Skip messages if cancellation is requested. Skipped and unack'd messages will be automatically re-queued by RabbitMQ once the channel closes.
            if (combinedCancellationToken.IsCancellationRequested)
            {
                return;
            }

            _logger.LogTrace("Received message #{DeliveryTag}", ea.DeliveryTag);

            // Extract properties and headers
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            var (properties, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(ea.BasicProperties);
            var bodySizeBytes = Encoding.UTF8.GetByteCount(body);

            var message = new RetrievedMessage
            {
                Body = body,
                Properties = properties,
                Headers = headers,
                Exchange = ea.Exchange,
                RoutingKey = ea.RoutingKey,
                Queue = queue,
                DeliveryTag = ea.DeliveryTag,
                Redelivered = ea.Redelivered,
                BodySizeBytes = bodySizeBytes,
                BodySize = OutputUtilities.ToSizeString(bodySizeBytes)
            };

            await receiveChan.Writer.WriteAsync(message, CancellationToken.None);

            // Check if we reached the message count limit
            if (counter.Increment() == messageCount)
            {
                _logger.LogDebug("Message limit {MessageCount} reached - initiating cancellation!", messageCount);
                await messageLimitCts.CancelAsync();
            }
        }
    }

    private void RegisterCancellationHandler(
        CancellationToken combinedCancellationToken,
        CancellationToken messageLimitToken,
        CancellationToken userCancellationToken,
        IChannel channel,
        string consumerTag,
        Channel<RetrievedMessage> receiveChan)
    {
        // Hook up callback that cancels the RabbitMQ consumer and completes the receive channel
        // when cancellation is requested (Ctrl+C or message count limit reached)
        combinedCancellationToken.Register(() =>
        {
            // Determine the cancellation reason for logging
            var isUserCancellation = userCancellationToken.IsCancellationRequested;
            var isMessageLimitReached = messageLimitToken.IsCancellationRequested && !isUserCancellation;

            _logger.LogDebug(
                "Cancellation requested - stopping RabbitMQ consumer (tag: {ConsumerTag}, reason: {Reason})",
                consumerTag,
                isUserCancellation ? "User cancellation (Ctrl+C)" : isMessageLimitReached ? "Message count limit reached" : "Unknown");

            // Cancel the consumer asynchronously to stop receiving new messages from RabbitMQ
            // Using fire-and-forget pattern as CancellationToken.Register doesn't support async callbacks
            _ = Task.Run(async () =>
            {
                try
                {
                    await channel.BasicCancelAsync(consumerTag, cancellationToken: CancellationToken.None);
                    _logger.LogDebug("RabbitMQ consumer cancelled successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cancel RabbitMQ consumer: {Message}", ex.Message);
                }
            }, CancellationToken.None);

            // Complete the receive channel so the writer task can finish processing remaining messages
            receiveChan.Writer.TryComplete();
            _logger.LogDebug("Receive channel completed");
        });
    }
}