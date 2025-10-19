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
    Task<int> ConsumeMessages(CancellationToken cancellationToken = default);
}

public class ConsumeService : IConsumeService
{
    private readonly ILogger<ConsumeService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRabbitChannelFactory _rabbitChannelFactory;
    private readonly IStatusOutputService _statusOutput;
    private readonly IConsumeOutputService _resultOutput;
    private readonly FileConfig _fileConfig;
    private readonly ConsumeOptions _consumeOptions;
    private readonly OutputOptions _outputOptions;

    public ConsumeService(
        ILogger<ConsumeService> logger,
        ILoggerFactory loggerFactory,
        IRabbitChannelFactory rabbitChannelFactory,
        IStatusOutputService statusOutput,
        IConsumeOutputService resultOutput,
        FileConfig fileConfig,
        ConsumeOptions consumeOptions,
        OutputOptions outputOptions)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _rabbitChannelFactory = rabbitChannelFactory;
        _statusOutput = statusOutput;
        _resultOutput = resultOutput;
        _fileConfig = fileConfig;
        _consumeOptions = consumeOptions;
        _outputOptions = outputOptions;
    }

    public async Task<int> ConsumeMessages(CancellationToken userCancellationToken = default)
    {
        var startTime = System.Diagnostics.Stopwatch.GetTimestamp();

        var queue = _consumeOptions.Queue;
        var ackMode = _consumeOptions.AckMode;
        var messageCount = _consumeOptions.MessageCount;

        _logger.LogDebug("Initiating consume operation: queue={Queue}, mode={AckMode}, count={messageCount}",
            queue, ackMode, messageCount);

        await using var channel = await _rabbitChannelFactory.GetChannelAsync();

        if (await ValidateQueueExists(channel, queue, userCancellationToken) is null)
        {
            // Queue does not exist, error already logged
            return 1; 
        }

        await ConfigureChannelQoS(channel, userCancellationToken);

        var (messageLimitCts, combinedCts) = CreateCombinedCancellationToken(userCancellationToken);
        var (receiveChan, ackChan) = CreateMessageChannels();
        var (consumer, receivedCount) = CreateConsumer(channel, receiveChan, messageCount, messageLimitCts, combinedCts.Token);

        ShowConsumingStatus(queue, messageCount);

        var consumerTag = await channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer, userCancellationToken);

        // Register cancellation handler to stop consumer and complete channels gracefully
        RegisterCancellationHandler(combinedCts.Token, messageLimitCts.Token, userCancellationToken, channel, consumerTag, receiveChan);

        var outputResult = await RunMessageProcessingPipeline(receiveChan, ackChan, channel, ackMode, messageCount,
            userCancellationToken);

        var endTime = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsedTime = System.Diagnostics.Stopwatch.GetElapsedTime(startTime, endTime);

        ShowCompletionStatus(receivedCount.Value, userCancellationToken.IsCancellationRequested, elapsedTime);

        // Build and output consume summary
        var response = BuildConsumeResponse(
            queue,
            ackMode,
            receivedCount.Value,
            outputResult,
            elapsedTime,
            userCancellationToken.IsCancellationRequested);

        _resultOutput.WriteConsumeResult(response);

        await channel.CloseAsync(userCancellationToken);

        return 0;
    }

    private async Task ConfigureChannelQoS(IChannel channel, CancellationToken userCancellationToken)
    {
        // Configure QoS with prefetch count
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _consumeOptions.PrefetchCount,
            global: false,
            userCancellationToken);

        _logger.LogDebug("Configured QoS with prefetch count: {PrefetchCount}", _consumeOptions.PrefetchCount);

        // Show warning if using requeue mode with unlimited prefetch
        if (_consumeOptions.AckMode == AckModes.Requeue && _consumeOptions.MessageCount <= 0)
        {
            _statusOutput.ShowWarning(
                "Using requeue mode without a message count may lead to memory issues due to unacknowledged messages accumulating (unbound buffer growth).");
        }
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

    private async Task<QueueInfo?> ValidateQueueExists(IChannel channel, string queue, CancellationToken userCancellationToken)
    {
        try
        {
            var queueDeclareOk = await channel.QueueDeclarePassiveAsync(queue, userCancellationToken);
            _logger.LogDebug("Queue '{Queue}' exists with {MessageCount} messages and {ConsumerCount} consumers",
                queueDeclareOk.QueueName, queueDeclareOk.MessageCount, queueDeclareOk.ConsumerCount);
            return QueueInfo.Create(queueDeclareOk);
        }
        catch (OperationInterruptedException ex)
        {
            _logger.LogError(ex, "Queue '{Queue}' not found. Reply code: {ReplyCode}, Reply text: {ReplyText}",
                queue, ex.ShutdownReason?.ReplyCode, ex.ShutdownReason?.ReplyText);

            var queueNotFoundError = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queue);
            _statusOutput.ShowError("Failed to consume from queue", queueNotFoundError);

            return null;
        }
    }

    private (CancellationTokenSource MessageLimitCts, CancellationTokenSource CombinedCts) CreateCombinedCancellationToken(
        CancellationToken userCancellationToken)
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

    private (AsyncEventingBasicConsumer Consumer, ReceivedMessageCounter Counter) CreateConsumer(
        IChannel channel,
        Channel<RabbitMessage> receiveChan,
        int messageCount,
        CancellationTokenSource messageLimitCts,
        CancellationToken combinedCancellationToken)
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

            await Task.Delay(TimeSpan.FromSeconds(2), combinedCancellationToken);
            var message = new RabbitMessage(
                ea.Exchange,
                ea.RoutingKey,
                _consumeOptions.Queue,
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

    private async Task<MessageOutputResult> RunMessageProcessingPipeline(
        Channel<RabbitMessage> receiveChan,
        Channel<(ulong, AckModes)> ackChan,
        IChannel channel,
        AckModes ackMode,
        int messageCount,
        CancellationToken cancellationToken)
    {
        // Create message output handler using factory
        var messageOutput = MessageOutputFactory.Create(
            _loggerFactory,
            _outputOptions,
            _fileConfig,
            messageCount);

        // Start processing received messages
        var writerTask = Task.Run(() =>
            messageOutput.WriteMessagesAsync(receiveChan, ackChan, ackMode, cancellationToken), CancellationToken.None);

        // Start dispatcher for acknowledgments of successfully processed messages
        var ackDispatcher = Task.Run(() => HandleAcks(ackChan, channel), CancellationToken.None);

        await Task.WhenAll(writerTask, ackDispatcher);

        return writerTask.Result;
    }

    private ConsumeResponse BuildConsumeResponse(
        string queue,
        AckModes ackMode,
        long messagesReceived,
        MessageOutputResult outputResult,
        TimeSpan elapsedTime,
        bool wasCancelled)
    {
        var messagesPerSecond = elapsedTime.TotalSeconds > 0
            ? Math.Round(outputResult.ProcessedCount / elapsedTime.TotalSeconds, 2)
            : 0;

        var outputDestination = _outputOptions.OutputFile != null
            ? _outputOptions.OutputFile.Name
            : "STDOUT";

        var result = new ConsumeResult
        {
            MessagesReceived = messagesReceived,
            MessagesProcessed = outputResult.ProcessedCount,
            DurationMs = elapsedTime.TotalMilliseconds,
            Duration = OutputUtilities.GetElapsedTimeString(elapsedTime),
            AckMode = ackMode.ToString(),
            OutputDestination = outputDestination,
            OutputFormat = _outputOptions.Format.ToString().ToLower(),
            CancellationReason = wasCancelled ? "User cancellation (Ctrl+C)" : null,
            MessagesPerSecond = messagesPerSecond,
            TotalSizeBytes = outputResult.TotalBytes,
            TotalSize = OutputUtilities.ToSizeString(outputResult.TotalBytes)
        };

        return new ConsumeResponse
        {
            Status = "success",
            Timestamp = DateTime.UtcNow,
            Queue = queue,
            Result = result
        };
    }

    private void ShowCompletionStatus(long receivedCount, bool wasCancelledByUser, TimeSpan elapsedTime)
    {
        if (wasCancelledByUser)
        {
            _statusOutput.ShowWarning("Consumption cancelled by user", addNewLine: true);
        }

        _statusOutput.ShowSuccess(
            $"Consumed {OutputUtilities.GetMessageCountString(receivedCount, _statusOutput.NoColor)} in {OutputUtilities.GetElapsedTimeString(elapsedTime)}");
        _logger.LogDebug("Consumption stopped. Waiting for RabbitMQ channel to close");
    }
}