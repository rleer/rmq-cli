using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared;
using RmqCli.Shared.Output;

namespace RmqCli.Commands.MessageRetrieval;

public abstract class BaseMessageRetrievalService
{
    protected readonly ILogger Logger;
    protected readonly IStatusOutputService StatusOutput;
    protected readonly QueueValidator QueueValidator;
    protected readonly MessagePipeline MessagePipeline;
    protected readonly OutputOptions OutputOptions;
    protected readonly IRabbitChannelFactory RabbitChannelFactory;
    protected readonly MessageRetrievalResultOutputService ResultOutput;
    protected readonly MessageRetrievalOptions Options;
    protected readonly IMessageRetrievalStrategy Strategy;

    protected BaseMessageRetrievalService(
        ILogger logger,
        IStatusOutputService statusOutput,
        QueueValidator queueValidator,
        MessagePipeline messagePipeline,
        OutputOptions outputOptions,
        IRabbitChannelFactory rabbitChannelFactory,
        MessageRetrievalResultOutputService resultOutput,
        MessageRetrievalOptions options,
        IMessageRetrievalStrategy strategy)
    {
        Logger = logger;
        StatusOutput = statusOutput;
        QueueValidator = queueValidator;
        MessagePipeline = messagePipeline;
        OutputOptions = outputOptions;
        RabbitChannelFactory = rabbitChannelFactory;
        ResultOutput = resultOutput;
        Options = options;
        Strategy = strategy;
    }

    protected abstract Task<bool> BeforeRetrievalAsync(IChannel channel, QueueInfo queueInfo, CancellationToken cancellationToken);

    protected async Task<int> RetrieveMessagesAsync(CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();

        Logger.LogDebug("Starting message retrieval: mode={OperationName}, queue={Queue}, count={MessageCount}", Strategy.StrategyName.ToLower(), Options.Queue, Options.MessageCount);

        // Get RabbitMQ channel
        await using var channel = await RabbitChannelFactory.GetChannelAsync();

        var queueInfo = await QueueValidator.ValidateAsync(channel, Options.Queue, cancellationToken);
        if (queueInfo.HasError)
        {
            StatusOutput.ShowError($"Failed to retrieve messages from queue '{Options.Queue}'", queueInfo.QueueError);
            return 1;
        }

        // Pre-retrieval hook
        if (!await BeforeRetrievalAsync(channel, queueInfo, cancellationToken))
        {
            return 0;
        }

        ShowOperationStartingStatus(Options.Queue, Options.MessageCount);

        // Create channels for message processing pipeline
        var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
        var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();

        var (writerTask, ackTask) = MessagePipeline.StartPipeline(receiveChan, ackChan, channel, OutputOptions, Options.MessageCount, cancellationToken);

        var counter = new ReceivedMessageCounter();
        await Strategy.RetrieveMessagesAsync(channel, Options.Queue, receiveChan, Options.MessageCount, counter, cancellationToken);

        // Wait for pipeline tasks to complete
        await Task.WhenAll(writerTask, ackTask);

        var endTime = Stopwatch.GetTimestamp();
        var elapsedTime = Stopwatch.GetElapsedTime(startTime, endTime);

        // Show completion status
        ShowCompletionStatus(counter.Value, cancellationToken.IsCancellationRequested, elapsedTime);

        var response = BuildResponse(Options.Queue, Options.AckMode, counter.Value, writerTask.Result, elapsedTime, cancellationToken.IsCancellationRequested);
        ResultOutput.WriteMessageRetrievalResult(response);

        await channel.CloseAsync(cancellationToken);

        return 0;
    }

    private void ShowOperationStartingStatus(string queue, int messageCount)
    {
        Logger.LogDebug("Starting RabbitMQ consumer for queue '{Queue}'", queue);

        var formattedQueueName = StatusOutput.NoColor ? queue : $"[orange1]{queue}[/]";
        var formattedMode = StatusOutput.NoColor ? Strategy.StrategyName.ToLower() : $"[orange1]{Strategy.StrategyName.ToLower()}[/]";
        var statusMessage = messageCount > 0
            ? $"Retrieving up to {OutputUtilities.GetMessageCountString(messageCount, StatusOutput.NoColor)} from queue {formattedQueueName} in {formattedMode} mode (Ctrl+C to stop)"
            : $"Retrieving messages from queue {formattedQueueName} in {formattedMode} mode (Ctrl+C to stop)";
        StatusOutput.ShowStatus(statusMessage);
    }


    private void ShowCompletionStatus(long retrievalCount, bool wasCancelledByUser, TimeSpan elapsedTime)
    {
        if (wasCancelledByUser)
        {
            StatusOutput.ShowWarning("Message retrieval cancelled by user", addNewLine: true);
        }

        StatusOutput.ShowSuccess(
            $"Retrieved {OutputUtilities.GetMessageCountString(retrievalCount, StatusOutput.NoColor)} in {OutputUtilities.GetElapsedTimeString(elapsedTime)}");
        Logger.LogDebug("Message retrieval stopped (mode={OperationName}). Waiting for RabbitMQ channel to close", Strategy.StrategyName);
    }

    private MessageRetrievalResponse BuildResponse(
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

        var result = new MessageRetrievalResult
        {
            MessagesReceived = messagesReceived,
            MessagesProcessed = outputResult.ProcessedCount,
            DurationMs = elapsedTime.TotalMilliseconds,
            Duration = OutputUtilities.GetElapsedTimeString(elapsedTime),
            AckMode = ackMode.ToString(),
            RetrievalMode = Strategy.StrategyName.ToLower(),
            CancellationReason = wasCancelled ? "User cancellation (Ctrl+C)" : null,
            MessagesPerSecond = messagesPerSecond,
            TotalSizeBytes = outputResult.TotalBytes,
            TotalSize = OutputUtilities.ToSizeString(outputResult.TotalBytes)
        };

        return new MessageRetrievalResponse
        {
            Status = "success",
            Timestamp = DateTime.UtcNow,
            Queue = queue,
            Result = result
        };
    }
}