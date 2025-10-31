using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RmqCli.Commands.Consume;
using RmqCli.Core.Models;
using RmqCli.Core.Services;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared;

namespace RmqCli.Commands.Peek;

public interface IPeekService
{
    Task<int> PeekMessages(CancellationToken cancellationToken = default);
}

public class PeekService : IPeekService
{
    private readonly ILogger<PeekService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRabbitChannelFactory _rabbitChannelFactory;
    private readonly IStatusOutputService _statusOutput;
    private readonly IPeekOutputService _resultOutput;
    private readonly FileConfig _fileConfig;
    private readonly PeekOptions _peekOptions;
    private readonly OutputOptions _outputOptions;

    public PeekService(
        ILogger<PeekService> logger,
        ILoggerFactory loggerFactory,
        IRabbitChannelFactory rabbitChannelFactory,
        IStatusOutputService statusOutput,
        IPeekOutputService resultOutput,
        FileConfig fileConfig,
        PeekOptions peekOptions,
        OutputOptions outputOptions)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _rabbitChannelFactory = rabbitChannelFactory;
        _statusOutput = statusOutput;
        _resultOutput = resultOutput;
        _fileConfig = fileConfig;
        _peekOptions = peekOptions;
        _outputOptions = outputOptions;
    }

    public async Task<int> PeekMessages(CancellationToken userCancellationToken = default)
    {
        var startTime = System.Diagnostics.Stopwatch.GetTimestamp();

        var queue = _peekOptions.Queue;
        var messageCount = _peekOptions.MessageCount;

        _logger.LogDebug("Initiating peek operation: queue={Queue}, count={MessageCount}",
            queue, messageCount);

        await using var channel = await _rabbitChannelFactory.GetChannelAsync();

        var queueInfo = await ValidateQueueExists(channel, queue, userCancellationToken);
        if (queueInfo is null)
            return 1;

        // Warn if queue is empty
        if (queueInfo.MessageCount == 0)
        {
            _statusOutput.ShowWarning(
                "Target queue is empty, no messages will be peeked. Consider using 'rmq consume' instead.");
            return 0;
        }

        ShowPeekingStatus(queue, messageCount);

        var outputResult = await PeekMessagesFromQueue(channel, queue, messageCount, userCancellationToken);

        var endTime = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsedTime = System.Diagnostics.Stopwatch.GetElapsedTime(startTime, endTime);
        
        ShowCompletionStatus(messageCount, outputResult.ProcessedCount, userCancellationToken.IsCancellationRequested, elapsedTime);

        // Build and output peek summary
        var response = BuildPeekResponse(
            queue,
            outputResult.ProcessedCount,
            outputResult,
            elapsedTime,
            userCancellationToken.IsCancellationRequested);

        _resultOutput.WritePeekResult(response);

        await channel.CloseAsync(userCancellationToken);

        return 0;
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
            _statusOutput.ShowError("Failed to peek from queue", queueNotFoundError);

            return null;
        }
    }

    private void ShowPeekingStatus(string queue, int messageCount)
    {
        _logger.LogDebug("Starting peek operation for queue '{Queue}'", queue);

        // Warn if large message count
        if (messageCount > 300)
        {
            _statusOutput.ShowWarning(
                $"Peek mode uses polling (basic.get) which is inefficient and wasteful for large message counts. " +
                $"Peeking {messageCount} messages may take a while. Consider using 'rmq consume' instead.");
        }
        
        var formattedQueueName = _statusOutput.NoColor ? queue : $"[orange1]{queue}[/]";
        var statusMessage = messageCount > 0
            ? $"Peeking up to {OutputUtilities.GetMessageCountString(messageCount, _statusOutput.NoColor)} from queue {formattedQueueName} (Ctrl+C to stop)"
            : $"Peeking messages from queue {formattedQueueName} (Ctrl+C to stop)";
        _statusOutput.ShowStatus(statusMessage);
    }

    private async Task<MessageOutputResult> PeekMessagesFromQueue(
        IChannel channel,
        string queue,
        int messageCount,
        CancellationToken cancellationToken)
    {
        // Create message output handler using factory
        var messageOutput = MessageOutputFactory.Create(
            _loggerFactory,
            _outputOptions,
            _fileConfig,
            messageCount);

        // Create channels for message processing pipeline
        var receiveChan = Channel.CreateUnbounded<RabbitMessage>();
        var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, AckModes ackMode)>();

        // Start message writer task
        var writerTask = Task.Run(() =>
            messageOutput.WriteMessagesAsync(receiveChan, ackChan, AckModes.Requeue, cancellationToken), CancellationToken.None);

        // Start ack handler task (will requeue all messages)
        var ackTask = Task.Run(() => HandleAcks(ackChan, channel), CancellationToken.None);

        // Polling loop using BasicGetAsync
        long peekedCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Check if we've reached the message limit
            if (peekedCount >= messageCount)
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
            peekedCount++;
        }

        // Complete the channels
        receiveChan.Writer.TryComplete();
        _logger.LogDebug("Receive channel completed after peeking {Count} messages", peekedCount);

        // Wait for all tasks to complete
        await Task.WhenAll(writerTask, ackTask);

        return writerTask.Result;
    }

    private async Task HandleAcks(Channel<(ulong deliveryTag, AckModes ackMode)> ackChan, IChannel rmqChannel)
    {
        _logger.LogDebug("Starting acknowledgment dispatcher for peek (all messages will be requeued)");
        await foreach (var (deliveryTag, _) in ackChan.Reader.ReadAllAsync())
        {
            // Always requeue for peek operations
            _logger.LogTrace("Requeuing peeked message #{DeliveryTag}", deliveryTag);
            await rmqChannel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
        }

        _logger.LogDebug("Acknowledgement dispatcher finished");
    }

    private PeekResponse BuildPeekResponse(
        string queue,
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

        var result = new PeekResult
        {
            MessagesReceived = messagesReceived,
            MessagesProcessed = outputResult.ProcessedCount,
            DurationMs = elapsedTime.TotalMilliseconds,
            Duration = OutputUtilities.GetElapsedTimeString(elapsedTime),
            OutputDestination = outputDestination,
            OutputFormat = _outputOptions.Format.ToString().ToLower(),
            CancellationReason = wasCancelled ? "User cancellation (Ctrl+C)" : null,
            MessagesPerSecond = messagesPerSecond,
            TotalSizeBytes = outputResult.TotalBytes,
            TotalSize = OutputUtilities.ToSizeString(outputResult.TotalBytes)
        };

        return new PeekResponse
        {
            Status = "success",
            Timestamp = DateTime.UtcNow,
            Queue = queue,
            Result = result
        };
    }

    private void ShowCompletionStatus(int targetMessageCount, long peekedCount, bool wasCancelledByUser, TimeSpan elapsedTime)
    {
        if (wasCancelledByUser)
        {
            _statusOutput.ShowWarning("Peek operation cancelled by user", addNewLine: true);
        }
        else if (peekedCount < targetMessageCount)
        {
            _statusOutput.ShowWarning(
                $"Only {OutputUtilities.GetMessageCountString(peekedCount, _statusOutput.NoColor)} {(peekedCount == 1 ? "was" : "were")} available in queue.");
        }

        _statusOutput.ShowSuccess(
            $"Peeked {OutputUtilities.GetMessageCountString(peekedCount, _statusOutput.NoColor)} in {OutputUtilities.GetElapsedTimeString(elapsedTime)}");
        _logger.LogDebug("Peek operation stopped. Waiting for RabbitMQ channel to close");
    }
}