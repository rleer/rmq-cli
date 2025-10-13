using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RmqCli.Commands.Consume;
using RmqCli.Infrastructure.Output.Formatters;
using RmqCli.Shared;

namespace RmqCli.Infrastructure.Output.Console;

/// <summary>
/// Writes consumed messages to standard output (console)
/// </summary>
public class ConsoleOutput : MessageOutput
{
    private readonly ILogger<ConsoleOutput> _logger;
    private readonly OutputFormat _format;

    public ConsoleOutput(ILogger<ConsoleOutput> logger, OutputFormat format)
    {
        _logger = logger;
        _format = format;
    }

    public override async Task WriteMessagesAsync(
        Channel<RabbitMessage> messageChannel,
        Channel<(ulong deliveryTag, AckModes ackMode)> ackChannel,
        AckModes ackMode)
    {
        _logger.LogDebug("Starting console message output");

        await foreach (var message in messageChannel.Reader.ReadAllAsync())
        {
            try
            {
                var formattedMessage = FormatMessage(message);
                System.Console.Out.WriteLine(formattedMessage);

                await ackChannel.Writer.WriteAsync((message.DeliveryTag, ackMode));
                _logger.LogTrace("Message #{DeliveryTag} written to console", message.DeliveryTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write message #{DeliveryTag}: {Message}",
                    message.DeliveryTag, ex.Message);
                // Requeue on error
                await ackChannel.Writer.WriteAsync((message.DeliveryTag, AckModes.Requeue));
            }
        }

        ackChannel.Writer.TryComplete();
        _logger.LogDebug("Console message output completed");
    }

    private string FormatMessage(RabbitMessage message)
    {
        return _format switch
        {
            OutputFormat.Plain => TextMessageFormatter.FormatMessage(message),
            OutputFormat.Json => JsonMessageFormatter.FormatMessage(message),
            OutputFormat.Table => throw new NotImplementedException("Table format is not yet implemented"),
            _ => throw new UnreachableException($"Unexpected OutputFormat: {_format}")
        };
    }
}
