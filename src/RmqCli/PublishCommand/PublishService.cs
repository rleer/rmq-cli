using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RmqCli.Common;
using RmqCli.Configuration;

namespace RmqCli.PublishCommand;

public interface IPublishService
{
    Task<int> PublishMessage(DestinationInfo dest, List<string> messages, int burstCount = 1, CancellationToken cancellationToken = default);
    Task<int> PublishMessageFromFile(DestinationInfo dest, FileInfo fileInfo, int burstCount = 1, CancellationToken cancellationToken = default);
    Task<int> PublishMessageFromStdin(DestinationInfo dest, int burstCount = 1, CancellationToken cancellationToken = default);
}

public class PublishService : IPublishService
{
    private readonly IRabbitChannelFactory _rabbitChannelFactory;
    private readonly ILogger<PublishService> _logger;
    private readonly FileConfig _fileConfig;
    private readonly IStatusOutputService _statusOutput;
    private readonly IPublishOutputService _resultOutput;

    public PublishService(
        IRabbitChannelFactory rabbitChannelFactory,
        ILogger<PublishService> logger,
        FileConfig fileConfig,
        IStatusOutputService statusOutput,
        IPublishOutputService resultOutput)
    {
        _rabbitChannelFactory = rabbitChannelFactory;
        _logger = logger;
        _fileConfig = fileConfig;
        _statusOutput = statusOutput;
        _resultOutput = resultOutput;
    }

    /// <summary>
    /// Publishes a list of messages to the specified destination.
    /// Supports burst publishing, where each message can be published multiple times.
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="messages"></param>
    /// <param name="burstCount"></param>
    /// <param name="cancellationToken"></param>
    public async Task<int> PublishMessage(
        DestinationInfo dest,
        List<string> messages,
        int burstCount = 1,
        CancellationToken cancellationToken = default)
    {
        var startTime = Stopwatch.GetTimestamp();

        _logger.LogDebug(
            "Initiating publish operation: exchange={Exchange}, routing-key={RoutingKey}, queue={Queue}, msg-count={MessageCount}, burst-count={BurstCount}",
            dest.Exchange, dest.RoutingKey, dest.Queue, messages.Count, burstCount);

        await using var channel = await _rabbitChannelFactory.GetChannelWithPublisherConfirmsAsync();

        var totalMessageCount = messages.Count * burstCount;
        var messageCountString = OutputUtilities.GetMessageCountString(totalMessageCount, _statusOutput.NoColor);

        // Prepare the list to collect publish results
        var publishResults = new List<PublishOperationDto>();

        try
        {
            _statusOutput.ShowStatus($"Publishing {messageCountString} to {GetDestinationString(dest)}...");

            publishResults = await _statusOutput.ExecuteWithProgress(
                description: "Publishing messages",
                maxValue: totalMessageCount,
                workload: progress =>
                    PublishCore(messages, channel, dest, progress, burstCount, cancellationToken));

            var endTime = Stopwatch.GetTimestamp();
            var elapsedTime = Stopwatch.GetElapsedTime(startTime, endTime);

            _statusOutput.ShowSuccess($"Published {messageCountString} successfully in {OutputUtilities.GetElapsedTimeString(elapsedTime)}");

            var result = PublishResponseFactory.Success(dest, publishResults, elapsedTime);
            _resultOutput.WritePublishResult(result);
        }
        catch (AlreadyClosedException ex)
        {
            _logger.LogError(ex, "Publishing failed. Channel was already closed with shutdown reason: {ReplyText} ({ReplyCode})",
                ex.ShutdownReason?.ReplyText, ex.ShutdownReason?.ReplyCode);

            var errorText = ex.ShutdownReason?.ReplyText ?? ex.Message;

            if (errorText.Contains("not found"))
            {
                var exchangeNotFoundError = PublishErrorInfoFactory.ExchangeNotFoundErrorInfo();

                _statusOutput.ShowError($"Failed to publish to {GetDestinationString(dest)}", exchangeNotFoundError);
                return 1;
            }

            if (errorText.Contains("max size"))
            {
                var maxSizeExceededError = PublishErrorInfoFactory.MaxSizeExceededErrorInfo(errorText);

                _statusOutput.ShowError($"Failed to publish to {GetDestinationString(dest)}", maxSizeExceededError);
                return 1;
            }

            var genericError = ErrorInfoFactory.GenericErrorInfo(
                "An error occurred while publishing messages",
                "PUBLISH_ERROR",
                "Check RabbitMQ server logs or re-run with debug logs for more details",
                exception: ex);

            _statusOutput.ShowError($"Failed to publish to {GetDestinationString(dest)}", genericError);
            throw;
        }
        catch (PublishException ex)
        {
            if (ex.IsReturn)
            {
                _logger.LogDebug(ex, "Caught publish exception due to 'basic.return'");
                
                var noRouteError = PublishErrorInfoFactory.NoRouteErrorInfo(dest.Type == "queue");
                _statusOutput.ShowError($"Failed to publish to {GetDestinationString(dest)}", noRouteError);
                return 1;
            }

            var genericError = ErrorInfoFactory.GenericErrorInfo(
                "An error occurred while publishing messages",
                "PUBLISH_ERROR",
                "Check RabbitMQ server logs or re-run with debug logs for more details",
                exception: ex);

            _statusOutput.ShowError($"Failed to publish to {GetDestinationString(dest)}", genericError);
            _logger.LogDebug("Caught publish exception that is not due to 'basic.return'");
            throw;
        }
        catch (OperationCanceledException)
        {
            var endTime = Stopwatch.GetTimestamp();
            var elapsedTime = Stopwatch.GetElapsedTime(startTime, endTime);

            _logger.LogDebug("Publishing operation canceled.");
            _statusOutput.ShowWarning("Publishing cancelled by user", addNewLine: true);

            var successCount = publishResults.Count;
            var failCount = messages.Count - successCount;
            if (successCount > 0)
            {
                _statusOutput.ShowSuccess(
                    $"Published {OutputUtilities.GetMessageCountString(successCount, _statusOutput.NoColor)} successfully before cancelled ({OutputUtilities.GetElapsedTimeString(elapsedTime)})");

                var partialResult = PublishResponseFactory.Partial(dest, publishResults, failCount, elapsedTime);
                _resultOutput.WritePublishResult(partialResult);
            }
            else
            {
                _statusOutput.ShowError("No messages were published before cancellation");

                var errorResult = PublishResponseFactory.Failure(dest, messages.Count, elapsedTime);
                _resultOutput.WritePublishResult(errorResult);
            }
        }
        finally
        {
            await channel.CloseAsync(cancellationToken: cancellationToken);
            await _rabbitChannelFactory.CloseConnectionAsync();
        }

        return 0;
    }

    public async Task<int> PublishMessageFromFile(DestinationInfo dest, FileInfo fileInfo, int burstCount = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading messages from file: {FilePath}", fileInfo.FullName);
        var messageBlob = await File.ReadAllTextAsync(fileInfo.FullName, cancellationToken);

        var (messages, delimiterDisplay) = SplitMessages(messageBlob);

        _logger.LogDebug("Read {MessageCount} messages from '{FilePath}' with delimiter '{MessageDelimiter}'", messages.Count, fileInfo.FullName,
            delimiterDisplay);

        return await PublishMessage(dest, messages, burstCount, cancellationToken);
    }

    public async Task<int> PublishMessageFromStdin(DestinationInfo dest, int burstCount = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading messages from STDIN");
        var messageBlob = await Console.In.ReadToEndAsync(cancellationToken);

        var (messages, delimiterDisplay) = SplitMessages(messageBlob);

        _logger.LogDebug("Read {MessageCount} messages from STDIN with delimiter '{MessageDelimiter}'", messages.Count,
            delimiterDisplay);

        return await PublishMessage(dest, messages, burstCount, cancellationToken);
    }

    private async Task<List<PublishOperationDto>> PublishCore(
        List<string> messages,
        IChannel channel,
        DestinationInfo dest,
        IProgress<int>? progress = null,
        int burstCount = 1,
        CancellationToken cancellationToken = default)
    {
        var messageBaseId = GetMessageId();
        var results = new List<PublishOperationDto>();
        var currentProgress = 0;

        for (var m = 0; m < messages.Count; m++)
        {
            var body = Encoding.UTF8.GetBytes(messages[m]);
            var messageIdSuffix = GetMessageIdSuffix(m, messages.Count);
            for (var i = 0; i < burstCount; i++)
            {
                var burstSuffix = burstCount > 1 ? GetMessageIdSuffix(i, burstCount) : string.Empty;
                var messageId = $"{messageBaseId}{messageIdSuffix}{burstSuffix}";
                var props = new BasicProperties
                {
                    MessageId = messageId,
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                await channel.BasicPublishAsync(
                    exchange: dest.Exchange ?? string.Empty,
                    routingKey: dest.Queue ?? dest.RoutingKey ?? string.Empty,
                    mandatory: true,
                    basicProperties: props,
                    body: body,
                    cancellationToken: cancellationToken);

                // Collect the result
                results.Add(new PublishOperationDto(props.MessageId, body.LongLength, props.Timestamp));

                // Report progress
                currentProgress++;
                progress?.Report(currentProgress);
            }
        }

        return results;
    }

    private (List<string> messages, string delimiterDisplay) SplitMessages(string messageBlob)
    {
        var messages = messageBlob
            .Split(_fileConfig.MessageDelimiter)
            .Select(m => m.Trim())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToList();

        var delimiterDisplay = string.Join("", _fileConfig.MessageDelimiter.Select(c => c switch
        {
            '\r' => "\\r",
            '\n' => "\\n",
            '\t' => "\\t",
            _ => c.ToString()
        }));
        return (messages, delimiterDisplay);
    }

    private static string GetDestinationString(DestinationInfo dest, bool useColor = true)
    {
        var colorPrefix = useColor ? "[orange1]" : string.Empty;
        var colorSuffix = useColor ? "[/]" : string.Empty;
        if (!string.IsNullOrEmpty(dest.Queue))
        {
            return $"queue {colorPrefix}'{dest.Queue}'{colorSuffix}";
        }

        if (!string.IsNullOrEmpty(dest.Exchange) && !string.IsNullOrEmpty(dest.RoutingKey))
        {
            return $"exchange {colorPrefix}'{dest.Exchange}'{colorSuffix} with routing key {colorPrefix}'{dest.RoutingKey}'{colorSuffix}";
        }

        return string.Empty;
    }

    /// <summary>
    /// Generates a unique message ID.
    /// </summary>
    /// <example>msg-e3955d32-5461</example>
    /// <returns>Message ID</returns>
    private static string GetMessageId()
    {
        return $"msg-{Guid.NewGuid().ToString("D")[..13]}";
    }

    /// <summary>
    /// Generates a suffix for the message ID based on the message index and total messages.
    /// </summary>
    /// <param name="messageIndex"></param>
    /// <param name="totalMessages"></param>
    /// <example>-001</example>
    /// <returns>Message ID suffix</returns>
    private static string GetMessageIdSuffix(int messageIndex, int totalMessages)
    {
        return "-" + $"{messageIndex + 1}".PadLeft(OutputUtilities.GetDigitCount(totalMessages), '0');
    }
}