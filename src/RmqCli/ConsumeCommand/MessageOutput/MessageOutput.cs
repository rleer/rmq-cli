using System.Threading.Channels;

namespace RmqCli.ConsumeCommand.MessageOutput;

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
    /// <param name="ackMode">The acknowledgment mode to use for successfully processed messages</param>
    public abstract Task WriteMessagesAsync(
        Channel<RabbitMessage> messageChannel,
        Channel<(ulong deliveryTag, AckModes ackMode)> ackChannel,
        AckModes ackMode);
}
