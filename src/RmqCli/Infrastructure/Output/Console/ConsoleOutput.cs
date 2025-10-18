using System.Diagnostics;
using System.Text;
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
    private readonly bool _compact;

    public ConsoleOutput(ILogger<ConsoleOutput> logger, OutputFormat format, bool compact = false)
    {
        _logger = logger;
        _format = format;
        _compact = compact;
    }

    public override async Task<MessageOutputResult> WriteMessagesAsync(
        Channel<RabbitMessage> messageChannel,
        Channel<(ulong deliveryTag, AckModes ackMode)> ackChannel,
        AckModes ackMode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting console message output");

        long processedCount = 0;
        long totalBytes = 0;

        await foreach (var message in messageChannel.Reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                var formattedMessage = FormatMessage(message);
                await System.Console.Out.WriteLineAsync(formattedMessage);

                await ackChannel.Writer.WriteAsync((message.DeliveryTag, ackMode), CancellationToken.None);
                _logger.LogTrace("Message #{DeliveryTag} written to console", message.DeliveryTag);

                // Track metrics
                totalBytes += Encoding.UTF8.GetByteCount(message.Body);
                processedCount++;

                // Check for cancellation after processing current message
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Cancellation requested - stopping after current message");
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write message #{DeliveryTag}: {Message}",
                    message.DeliveryTag, ex.Message);
                // Requeue on error
                await ackChannel.Writer.WriteAsync((message.DeliveryTag, AckModes.Requeue), CancellationToken.None);
            }
        }

        ackChannel.Writer.TryComplete();
        _logger.LogDebug("Console message output completed (processed: {ProcessedCount})", processedCount);

        return new MessageOutputResult(processedCount, totalBytes);
    }

    private string FormatMessage(RabbitMessage message)
    {
        return _format switch
        {
            OutputFormat.Plain => TextMessageFormatter.FormatMessage(message),
            OutputFormat.Json => JsonMessageFormatter.FormatMessage(message),
            OutputFormat.Table => TableMessageFormatter.FormatMessage(message, compact: _compact),
            _ => throw new UnreachableException($"Unexpected OutputFormat: {_format}")
        };
    }
}