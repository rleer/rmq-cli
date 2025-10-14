using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RmqCli.Commands.Consume;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output.Formatters;
using RmqCli.Shared;

namespace RmqCli.Infrastructure.Output.File;

/// <summary>
/// Writes consumed messages to file(s).
/// Supports single file mode or rotating file mode based on configuration.
/// </summary>
public class FileOutput : MessageOutput
{
    private readonly ILogger<FileOutput> _logger;
    private readonly FileInfo _outputFileInfo;
    private readonly OutputFormat _format;
    private readonly FileConfig _fileConfig;
    private readonly bool _useRotatingFiles;

    public FileOutput(
        ILogger<FileOutput> logger,
        FileInfo outputFileInfo,
        OutputFormat format,
        FileConfig fileConfig,
        int messageCount)
    {
        _logger = logger;
        _outputFileInfo = outputFileInfo;
        _format = format;
        _fileConfig = fileConfig;

        // Use rotating files if message count is unlimited or exceeds messages per file
        _useRotatingFiles = messageCount == -1 || messageCount > fileConfig.MessagesPerFile;
    }

    public override async Task<MessageOutputResult> WriteMessagesAsync(
        Channel<RabbitMessage> messageChannel,
        Channel<(ulong deliveryTag, AckModes ackMode)> ackChannel,
        AckModes ackMode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting file message output (rotating: {UseRotating})", _useRotatingFiles);

        MessageOutputResult result;
        try
        {
            if (_useRotatingFiles)
            {
                result = await WriteRotatingFilesAsync(messageChannel, ackChannel, ackMode, cancellationToken);
            }
            else
            {
                result = await WriteSingleFileAsync(messageChannel, ackChannel, ackMode, cancellationToken);
            }
        }
        finally
        {
            ackChannel.Writer.TryComplete();
            _logger.LogDebug("File message output completed");
        }

        return result;
    }

    private async Task<MessageOutputResult> WriteSingleFileAsync(
        Channel<RabbitMessage> messageChannel,
        Channel<(ulong deliveryTag, AckModes ackMode)> ackChannel,
        AckModes ackMode,
        CancellationToken cancellationToken)
    {
        await using var fileStream = _outputFileInfo.OpenWrite();
        await using var writer = new StreamWriter(fileStream);

        var isFirstMessage = true;
        long processedCount = 0;
        long totalBytes = 0;

        await foreach (var message in messageChannel.Reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                // Add delimiter between messages for plain text format
                if (!isFirstMessage && _format == OutputFormat.Plain)
                {
                    await writer.WriteLineAsync(_fileConfig.MessageDelimiter);
                }
                isFirstMessage = false;

                var formattedMessage = FormatMessage(message);
                await writer.WriteLineAsync(formattedMessage);

                await ackChannel.Writer.WriteAsync((message.DeliveryTag, ackMode), CancellationToken.None);
                _logger.LogTrace("Message #{DeliveryTag} written to file", message.DeliveryTag);

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
                throw;
            }
        }

        await writer.FlushAsync(CancellationToken.None);
        return new MessageOutputResult(processedCount, totalBytes);
    }

    private async Task<MessageOutputResult> WriteRotatingFilesAsync(
        Channel<RabbitMessage> messageChannel,
        Channel<(ulong deliveryTag, AckModes ackMode)> ackChannel,
        AckModes ackMode,
        CancellationToken cancellationToken)
    {
        StreamWriter? writer = null;
        long processedCount = 0;
        long totalBytes = 0;

        try
        {
            var fileIndex = 0;
            var messagesInCurrentFile = 0;
            var baseFileName = Path.Combine(
                _outputFileInfo.DirectoryName ?? string.Empty,
                Path.GetFileNameWithoutExtension(_outputFileInfo.Name));
            var fileExtension = _outputFileInfo.Extension;

            await foreach (var message in messageChannel.Reader.ReadAllAsync(CancellationToken.None))
            {
                try
                {
                    // Open new file if needed
                    if (writer is null || messagesInCurrentFile >= _fileConfig.MessagesPerFile)
                    {
                        if (writer is not null)
                        {
                            await writer.FlushAsync();
                            await writer.DisposeAsync();
                        }

                        var currentFileName = $"{baseFileName}.{fileIndex++}{fileExtension}";
                        _logger.LogDebug("Creating new file: {FileName}", currentFileName);

                        writer = new StreamWriter(currentFileName);
                        messagesInCurrentFile = 0;
                    }

                    // Add delimiter between messages in same file for plain text format
                    if (messagesInCurrentFile > 0 && _format == OutputFormat.Plain)
                    {
                        await writer.WriteLineAsync(_fileConfig.MessageDelimiter);
                    }

                    var formattedMessage = FormatMessage(message);
                    await writer.WriteLineAsync(formattedMessage);
                    messagesInCurrentFile++;

                    await ackChannel.Writer.WriteAsync((message.DeliveryTag, ackMode), CancellationToken.None);
                    _logger.LogTrace("Message #{DeliveryTag} written to rotating file", message.DeliveryTag);

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
                    throw;
                }
            }
        }
        finally
        {
            if (writer is not null)
            {
                await writer.DisposeAsync();
            }
        }

        return new MessageOutputResult(processedCount, totalBytes);
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
