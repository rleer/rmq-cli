using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output;
using RmqCli.Shared;

namespace RmqCli.Commands.MessageRetrieval;

public class MessagePipeline
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly FileConfig _fileConfig;
    private readonly AckHandler _ackHandler;

    public MessagePipeline(ILoggerFactory loggerFactory, FileConfig fileConfig, AckHandler ackHandler)
    {
        _loggerFactory = loggerFactory;
        _fileConfig = fileConfig;
        _ackHandler = ackHandler;
    }

    public (Task<MessageOutputResult> writerTask, Task ackTask) StartPipeline(
        Channel<RetrievedMessage> receiveChan,
        Channel<(ulong deliveryTag, bool success)> ackChan,
        IChannel channel,
        OutputOptions outputOptions,
        int messageCount,
        CancellationToken cancellationToken)
    {
        // Create message output handler using factory
        var messageOutput = MessageOutputFactory.Create(
            _loggerFactory,
            outputOptions,
            _fileConfig,
            messageCount);

        // Start message writer task
        var writerTask = Task.Run(() =>
            messageOutput.WriteMessagesAsync(receiveChan, ackChan, cancellationToken), CancellationToken.None);

        // Start acknowledgment dispatcher
        var ackTask = Task.Run(() => _ackHandler.DispatchAcknowledgments(ackChan, channel), CancellationToken.None);

        return (writerTask, ackTask);
    }
}