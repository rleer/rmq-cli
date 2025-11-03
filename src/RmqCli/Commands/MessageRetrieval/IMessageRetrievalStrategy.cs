using System.Threading.Channels;
using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Commands.MessageRetrieval;

public interface IMessageRetrievalStrategy
{
    /// <summary>
    /// Retrieves messages from the specified queue and writes them to a channel for further processing.
    /// </summary>
    Task RetrieveMessagesAsync(
        IChannel channel,
        string queue,
        Channel<RetrievedMessage> receiveChan,
        int messageCount,
        ReceivedMessageCounter counter,
        CancellationToken cancellationToken);

    // The name of the operation, e.g., "polling", "subscriber"
    string StrategyName { get; }
}