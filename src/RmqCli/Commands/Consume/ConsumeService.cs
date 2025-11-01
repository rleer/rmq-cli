using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Core.Models;
using RmqCli.Core.Services;
using RmqCli.Infrastructure.Output;
using RmqCli.Shared;

namespace RmqCli.Commands.Consume;

public interface IConsumeService
{
    Task<int> ConsumeMessages(CancellationToken cancellationToken = default);
}

public class ConsumeService : BaseMessageRetrievalService, IConsumeService
{
    public ConsumeService(
        ILogger<ConsumeService> logger,
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

    protected override async Task<bool> BeforeRetrievalAsync(IChannel channel, QueueInfo queueInfo, CancellationToken cancellationToken)
    {
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: Options.PrefetchCount,
            global: false,
            cancellationToken);

        Logger.LogDebug("Configured QoS with prefetch count: {PrefetchCount}", Options.PrefetchCount);

        // Show warning if using requeue mode with unlimited prefetch
        if (Options.AckMode == AckModes.Requeue && Options.MessageCount <= 0)
        {
            StatusOutput.ShowWarning(
                "Using requeue mode without a message count may lead to memory issues due to unacknowledged messages accumulating (unbound buffer growth).");
        }

        return true;
    }

    public Task<int> ConsumeMessages(CancellationToken cancellationToken = default)
    {
        return RetrieveMessagesAsync(cancellationToken);
    }
}