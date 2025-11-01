using System.Threading.Channels;
using RmqCli.Core.Models;

namespace RmqCli.Infrastructure.Output;

/// <summary>
/// Base class for writing consumed messages to different outputs (console, file, etc.)
/// with different formatting (text, JSON, etc.)
/// </summary>
public abstract class MessageOutput
{
    /// <summary>
    /// Reads messages from the message channel, formats and writes them to the output,
    /// and sends acknowledgments to the ack channel.
    /// </summary>
    /// <param name="messageChannel">Channel containing messages to write</param>
    /// <param name="ackChannel">Channel for sending acknowledgment information</param>
    /// <param name="cancellationToken">Token to signal graceful shutdown after current message</param>
    /// <returns>Statistics about the processed messages</returns>
    public abstract Task<MessageOutputResult> WriteMessagesAsync(
        Channel<RabbitMessage> messageChannel,
        Channel<(ulong deliveryTag, bool success)> ackChannel,
        CancellationToken cancellationToken = default);
}