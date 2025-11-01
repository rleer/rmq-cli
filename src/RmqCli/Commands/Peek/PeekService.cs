using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Core.Models;
using RmqCli.Core.Services;
using RmqCli.Infrastructure.Output;
using RmqCli.Shared;

namespace RmqCli.Commands.Peek;

public interface IPeekService
{
    Task<int> PeekMessages(CancellationToken cancellationToken = default);
}

public class PeekService : BaseMessageRetrievalService, IPeekService
{
    public PeekService(
        ILogger<PeekService> logger,
        IStatusOutputService statusOutput,
        QueueValidator queueValidator,
        MessagePipeline messagePipeline,
        OutputOptions outputOptions,
        IRabbitChannelFactory rabbitChannelFactory,
        MessageRetrievalResultOutputService resultOutput,
        MessageRetrievalOptions options,
        IMessageRetrievalStrategy strategy) : base(logger, statusOutput, queueValidator, messagePipeline, outputOptions, rabbitChannelFactory,
        resultOutput, options, strategy)
    {
    }

    protected override Task<bool> BeforeRetrievalAsync(IChannel channel, QueueInfo queueInfo, CancellationToken cancellationToken)
    {
        // Warn if queue is empty
        if (queueInfo.MessageCount == 0)
        {
            StatusOutput.ShowWarning(
                "Target queue is empty, no messages will be peeked. Consider using 'rmq consume' instead.");
            return Task.FromResult(false);
        }

        // Warn if large message count
        if (Options.MessageCount >= 1000)
        {
            StatusOutput.ShowWarning(
                $"Peek mode uses polling (basic.get) which is inefficient and wasteful for large message counts. " +
                $"Peeking {Options.MessageCount} messages may take a while. Consider using 'rmq consume' instead.");
        }

        return Task.FromResult(true);
    }

    public Task<int> PeekMessages(CancellationToken cancellationToken = default)
    {
        return RetrieveMessagesAsync(cancellationToken);
    }
}