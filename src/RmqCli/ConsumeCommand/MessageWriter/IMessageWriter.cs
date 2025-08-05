using System.Threading.Channels;
using RmqCli.Common;

namespace RmqCli.ConsumeCommand.MessageWriter;

public interface IMessageWriter
{
    Task WriteMessageAsync(
        Channel<RabbitMessage> messageChannel,
        Channel<(ulong deliveryTag, AckModes ackMode)> ackChannel,
        AckModes ackMode);

    IMessageWriter Initialize(FileInfo? outputFileInfo, OutputFormat outputFormat = OutputFormat.Plain);
}