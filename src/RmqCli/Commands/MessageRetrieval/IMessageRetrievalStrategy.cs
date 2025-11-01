using System.Threading.Channels;
using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Commands.MessageRetrieval;

public interface IMessageRetrievalStrategy
{
    /// <summary>
    /// Retrieves messages from the specified queue and writes them to a dedicated channel.
    /// </summary>
    Task RetrieveMessagesAsync(
        IChannel channel,
        string queue,
        Channel<RabbitMessage> receiveChan,
        int messageCount,
        ReceivedMessageCounter counter,
        CancellationToken cancellationToken);

    // The name of the operation, e.g., "consume", "peek"
    string StrategyName { get; }
}