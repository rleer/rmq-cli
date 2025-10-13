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
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Initiating consume operation: queue={Queue}, mode={AckMode}, count={messageCount}",
            queue, ackMode, messageCount);

        // Create a local cancellation token source to allow stopping the consumer
        var localCts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, localCts.Token);

        await using var channel = await _rabbitChannelFactory.GetChannelAsync();

        // Ensure the specified queue exists
        try
        {
            await channel.QueueDeclarePassiveAsync(queue, linkedCts.Token);
        }
        catch (OperationInterruptedException ex)
        {
            _logger.LogError(ex, "Queue '{Queue}' not found. Reply code: {ReplyCode}, Reply text: {ReplyText}",
                queue, ex.ShutdownReason?.ReplyCode, ex.ShutdownReason?.ReplyText);

            var queueNotFoundError = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queue);
            _statusOutput.ShowError($"Failed to consume from queue", queueNotFoundError);

            return 1;
        }

        var receiveChan = Channel.CreateUnbounded<RabbitMessage>();
        var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, AckModes ackMode)>();

        // Hook up callback that completes the receive-channel when message count is reached or cancellation is requested by user/applicaiton
        linkedCts.Token.Register(() =>
        {
            _logger.LogDebug("Completing receive channel (cancellation token status: application={ParentCt}, local={LocalCt})",
                cancellationToken.IsCancellationRequested, localCts.IsCancellationRequested);
            if (cancellationToken.IsCancellationRequested)
            {
                _statusOutput.ShowWarning("Consumption cancelled by user", addNewLine: true);
            }

            receiveChan.Writer.TryComplete();
        });

        long receivedCount = 0;

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            // Skip messages if cancellation is requested. Skipped and unack'd messages will be automatically re-queued by RabbitMQ once the channel closes.
            if (linkedCts.Token.IsCancellationRequested)
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

            await receiveChan.Writer.WriteAsync(message, linkedCts.Token);
            _logger.LogTrace("Message #{DeliveryTag} written to receive channel", ea.DeliveryTag);

            // Check if we reached the message count limit
            if (Interlocked.Increment(ref receivedCount) == messageCount)
            {
                _logger.LogDebug("Message limit {MessageCount} reached - initiating cancellation!", messageCount);
                await localCts.CancelAsync();
            }
        };

        _logger.LogDebug("Starting RabbitMQ consumer for queue '{Queue}'", queue);

        var formatedQueueName = _statusOutput.NoColor ? queue : $"[orange1]{queue}[/]";
        var statusMessage = messageCount > 0
            ? $"Consuming up to {OutputUtilities.GetMessageCountString(messageCount, _statusOutput.NoColor)} from queue '{formatedQueueName}' (Ctrl+C to stop)"
            : $"Consuming messages from queue '{formatedQueueName}' (Ctrl+C to stop)";
        _statusOutput.ShowStatus(statusMessage);

        // Start consuming messages from the specified queue
        _ = channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);

        // Create message output handler using factory
        var messageOutput = MessageOutputFactory.Create(
            _loggerFactory,
            outputFileInfo,
            outputFormat,
            _fileConfig,
            messageCount);

        // Start processing received messages
        var writerTask = Task.Run(() =>
            messageOutput.WriteMessagesAsync(receiveChan, ackChan, ackMode));

        // Start dispatcher for acknowledgments of successfully processed messages
        var ackDispatcher = Task.Run(() => HandleAcks(ackChan, channel));

        await Task.WhenAll(writerTask, ackDispatcher);

        _statusOutput.ShowSuccess($"Consumed {OutputUtilities.GetMessageCountString(receivedCount, _statusOutput.NoColor)}");
        _logger.LogDebug("Consumption stopped. Waiting for RabbitMQ channel to close");
        await channel.CloseAsync();
        
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
}