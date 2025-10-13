using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RmqCli.Core.Services;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared;

namespace RmqCli.Commands.Consume;

public interface IConsumeService
{
    Task<int> ConsumeMessages(
        string queue,
        AckModes ackMode,
        FileInfo? outputFileInfo = null,
        int messageCount = -1,
        OutputFormat outputFormat = OutputFormat.Plain,
        CancellationToken cancellationToken = default);
}

public class ConsumeService : IConsumeService
{
    private readonly ILogger<ConsumeService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRabbitChannelFactory _rabbitChannelFactory;
    private readonly IStatusOutputService _statusOutput;
    private readonly FileConfig _fileConfig;

    public ConsumeService(
        ILogger<ConsumeService> logger,
        ILoggerFactory loggerFactory,
        IRabbitChannelFactory rabbitChannelFactory,
        IStatusOutputService statusOutput,
        FileConfig fileConfig)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _rabbitChannelFactory = rabbitChannelFactory;
        _statusOutput = statusOutput;
        _fileConfig = fileConfig;
    }

    public async Task<int> ConsumeMessages(
        string queue,
        AckModes ackMode,
        FileInfo? outputFileInfo,
        int messageCount = -1,
        OutputFormat outputFormat = OutputFormat.Plain,
        CancellationToken userCancellationToken = default)
    {
        _logger.LogDebug("Initiating consume operation: queue={Queue}, mode={AckMode}, count={messageCount}",
            queue, ackMode, messageCount);

        await using var channel = await _rabbitChannelFactory.GetChannelAsync();

        if (!await ValidateQueueExists(channel, queue, userCancellationToken))
            return 1;

        var (messageLimitCts, combinedCts) = CreateCombinedCancellationToken(userCancellationToken);
        var (receiveChan, ackChan) = CreateMessageChannels();
        var (consumer, receivedCount) = CreateConsumer(channel, receiveChan, messageLimitCts, combinedCts.Token, messageCount);

        ShowConsumingStatus(queue, messageCount);

        var consumerTag = await channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer, userCancellationToken);

        // Register cancellation handler to stop consumer and complete channels gracefully
        RegisterCancellationHandler(combinedCts.Token, messageLimitCts.Token, userCancellationToken, channel, consumerTag, receiveChan);

        await RunMessageProcessingPipeline(receiveChan, ackChan, channel, outputFileInfo, outputFormat, ackMode, messageCount, userCancellationToken);

        ShowCompletionStatus(receivedCount.Value);
        await channel.CloseAsync(userCancellationToken);

        return 0;
    }

    private async Task HandleAcks(Channel<(ulong deliveryTag, AckModes ackMode)> ackChan, IChannel rmqChannel)
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

    private async Task<bool> ValidateQueueExists(IChannel channel, string queue, CancellationToken userCancellationToken)
    {
        try
        {
            await channel.QueueDeclarePassiveAsync(queue, userCancellationToken);
            return true;
        }
        catch (OperationInterruptedException ex)
        {
            _logger.LogError(ex, "Queue '{Queue}' not found. Reply code: {ReplyCode}, Reply text: {ReplyText}",
                queue, ex.ShutdownReason?.ReplyCode, ex.ShutdownReason?.ReplyText);

            var queueNotFoundError = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queue);
            _statusOutput.ShowError("Failed to consume from queue", queueNotFoundError);

            return false;
        }
    }

    private (CancellationTokenSource MessageLimitCts, CancellationTokenSource CombinedCts) CreateCombinedCancellationToken(CancellationToken userCancellationToken)
    {
        var messageLimitCts = new CancellationTokenSource();
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(userCancellationToken, messageLimitCts.Token);
        return (messageLimitCts, combinedCts);
    }

    private (Channel<RabbitMessage> ReceiveChannel, Channel<(ulong, AckModes)> AckChannel) CreateMessageChannels()
    {
        var receiveChan = Channel.CreateUnbounded<RabbitMessage>();
        var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, AckModes ackMode)>();

        return (receiveChan, ackChan);
    }

    private void RegisterCancellationHandler(
        CancellationToken combinedCancellationToken,
        CancellationToken messageLimitToken,
        CancellationToken userCancellationToken,
        IChannel channel,
        string consumerTag,
        Channel<RabbitMessage> receiveChan)
    {
        // Hook up callback that cancels the RabbitMQ consumer and completes the receive channel
        // when cancellation is requested (Ctrl+C or message count limit reached)
        combinedCancellationToken.Register(() =>
        {
            // Determine the cancellation reason for logging/user feedback
            var isUserCancellation = userCancellationToken.IsCancellationRequested;
            var isMessageLimitReached = messageLimitToken.IsCancellationRequested && !isUserCancellation;

            _logger.LogDebug(
                "Cancellation requested - stopping RabbitMQ consumer (tag: {ConsumerTag}, reason: {Reason})",
                consumerTag,
                isUserCancellation ? "User cancellation (Ctrl+C)" : isMessageLimitReached ? "Message count limit reached" : "Unknown");

            if (isUserCancellation)
            {
                _statusOutput.ShowWarning("Consumption cancelled by user", addNewLine: true);
            }

            // Cancel the consumer asynchronously to stop receiving new messages from RabbitMQ
            // Using fire-and-forget pattern as CancellationToken.Register doesn't support async callbacks
            _ = Task.Run(async () =>
            {
                try
                {
                    await channel.BasicCancelAsync(consumerTag);
                    _logger.LogDebug("RabbitMQ consumer cancelled successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cancel RabbitMQ consumer: {Message}", ex.Message);
                }
            });

            // Complete the receive channel so the writer task can finish processing remaining messages
            receiveChan.Writer.TryComplete();
            _logger.LogDebug("Receive channel completed");
        });
    }

    private (AsyncEventingBasicConsumer Consumer, ReceivedMessageCounter Counter) CreateConsumer(
        IChannel channel,
        Channel<RabbitMessage> receiveChan,
        CancellationTokenSource messageLimitCts,
        CancellationToken combinedCancellationToken,
        int messageCount)
    {
        var counter = new ReceivedMessageCounter();
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            // Skip messages if cancellation is requested. Skipped and unack'd messages will be automatically re-queued by RabbitMQ once the channel closes.
            if (combinedCancellationToken.IsCancellationRequested)
            {
                return;
            }

            _logger.LogTrace("Received message #{DeliveryTag}", ea.DeliveryTag);
            var message = new RabbitMessage(
                System.Text.Encoding.UTF8.GetString(ea.Body.ToArray()),
                ea.DeliveryTag,
                ea.BasicProperties,
                ea.Redelivered
            );

            await receiveChan.Writer.WriteAsync(message, CancellationToken.None);
            _logger.LogTrace("Message #{DeliveryTag} written to receive channel", ea.DeliveryTag);

            // Check if we reached the message count limit
            if (counter.Increment() == messageCount)
            {
                _logger.LogDebug("Message limit {MessageCount} reached - initiating cancellation!", messageCount);
                await messageLimitCts.CancelAsync();
            }
        };

        return (consumer, counter);
    }

    private sealed class ReceivedMessageCounter
    {
        private long _count;

        public long Increment() => Interlocked.Increment(ref _count);

        public long Value => Interlocked.Read(ref _count);
    }

    private void ShowConsumingStatus(string queue, int messageCount)
    {
        _logger.LogDebug("Starting RabbitMQ consumer for queue '{Queue}'", queue);

        var formattedQueueName = _statusOutput.NoColor ? queue : $"[orange1]{queue}[/]";
        var statusMessage = messageCount > 0
            ? $"Consuming up to {OutputUtilities.GetMessageCountString(messageCount, _statusOutput.NoColor)} from queue '{formattedQueueName}' (Ctrl+C to stop)"
            : $"Consuming messages from queue '{formattedQueueName}' (Ctrl+C to stop)";
        _statusOutput.ShowStatus(statusMessage);
    }

    private async Task RunMessageProcessingPipeline(
        Channel<RabbitMessage> receiveChan,
        Channel<(ulong, AckModes)> ackChan,
        IChannel channel,
        FileInfo? outputFileInfo,
        OutputFormat outputFormat,
        AckModes ackMode,
        int messageCount,
        CancellationToken cancellationToken)
    {
        // Create message output handler using factory
        var messageOutput = MessageOutputFactory.Create(
            _loggerFactory,
            outputFileInfo,
            outputFormat,
            _fileConfig,
            messageCount);

        // Start processing received messages
        var writerTask = Task.Run(() =>
            messageOutput.WriteMessagesAsync(receiveChan, ackChan, ackMode, cancellationToken), CancellationToken.None);

        // Start dispatcher for acknowledgments of successfully processed messages
        var ackDispatcher = Task.Run(() => HandleAcks(ackChan, channel), CancellationToken.None);

        await Task.WhenAll(writerTask, ackDispatcher);
    }

    private void ShowCompletionStatus(long receivedCount)
    {
        _statusOutput.ShowSuccess($"Consumed {OutputUtilities.GetMessageCountString(receivedCount, _statusOutput.NoColor)}");
        _logger.LogDebug("Consumption stopped. Waiting for RabbitMQ channel to close");
    }
}