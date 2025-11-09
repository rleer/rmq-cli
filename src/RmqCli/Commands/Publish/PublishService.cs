using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared.Factories;
using RmqCli.Shared.Output;
using PublishErrorInfoFactory = RmqCli.Shared.Factories.PublishErrorInfoFactory;

namespace RmqCli.Commands.Publish;

public interface IPublishService
{
    Task<int> PublishMessage(DestinationInfo dest, int burstCount = 1, CancellationToken cancellationToken = default);
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
    private readonly PublishOptions _options;

    public PublishService(
        IRabbitChannelFactory rabbitChannelFactory,
        ILogger<PublishService> logger,
        FileConfig fileConfig,
        IStatusOutputService statusOutput,
        IPublishOutputService resultOutput,
        PublishOptions options)
    {
        _rabbitChannelFactory = rabbitChannelFactory;
        _logger = logger;
        _fileConfig = fileConfig;
        _statusOutput = statusOutput;
        _resultOutput = resultOutput;
        _options = options;
    }

    /// <summary>
    /// Publishes a list of messages to the specified destination.
    /// Supports burst publishing, where each message can be published multiple times.
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="burstCount"></param>
    /// <param name="cancellationToken"></param>
    public async Task<int> PublishMessage(
        DestinationInfo dest,
        int burstCount = 1,
        CancellationToken cancellationToken = default)
    {
        // Check if inline JSON message is provided
        if (_options.JsonMessage != null)
        {
            _logger.LogDebug("Parsing inline JSON message");

            try
            {
                var jsonMessage = JsonMessageParser.ParseSingle(_options.JsonMessage);
                _logger.LogDebug("Parsed inline JSON message");

                // Merge JSON message with CLI options
                var mergedMessage = PropertyMerger.Merge(jsonMessage, _options);

                return await PublishMessageInternal(dest, [mergedMessage], burstCount, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Failed to parse inline JSON message");
                var errorInfo = ErrorInfoFactory.GenericErrorInfo(
                    ex.Message,
                    "INVALID_JSON",
                    "Ensure the inline JSON message is correctly formatted",
                    exception: ex);
                _statusOutput.ShowError($"Failed to parse inline JSON message", errorInfo);
                return 1;
            }
        }

        if (_options.MessageBody != null)
        {
            // Convert plain text messages to Message
            var messagesWithProps = ConvertToMessages([_options.MessageBody]);
            return await PublishMessageInternal(dest, messagesWithProps, burstCount, cancellationToken);
        }

        // Should not reach here due to prior validation
        _logger.LogError("No message body or file provided for publishing");
        return 1;
    }

    public async Task<int> PublishMessageFromFile(DestinationInfo dest, FileInfo fileInfo, int burstCount = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading messages from file: {FilePath}", fileInfo.FullName);
        var messageBlob = await File.ReadAllTextAsync(fileInfo.FullName, cancellationToken);

        // Auto-detect format: Try JSON first, fallback to plain text
        try
        {
            _logger.LogDebug("Attempting to parse file as JSON (NDJSON format)");
            var jsonMessages = JsonMessageParser.ParseNdjson(messageBlob);

            if (jsonMessages.Count > 0)
            {
                _logger.LogDebug("Detected JSON format: Parsed {MessageCount} JSON messages from '{FilePath}'", jsonMessages.Count, fileInfo.FullName);

                // Merge JSON messages with CLI options
                var mergedMessages = jsonMessages.Select(jsonMsg => PropertyMerger.Merge(jsonMsg, _options)).ToList();

                return await PublishMessageInternal(dest, mergedMessages, burstCount, cancellationToken);
            }
        }
        catch (ArgumentException)
        {
            // Not valid JSON, fallback to plain text
            _logger.LogDebug("JSON parsing failed, treating file as plain text");
        }

        // Plain text mode (delimiter-separated messages)
        _logger.LogDebug("Detected plain text format");
        var (messages, delimiterDisplay) = SplitMessages(messageBlob);

        _logger.LogDebug("Read {MessageCount} messages from '{FilePath}' with delimiter '{MessageDelimiter}'", messages.Count, fileInfo.FullName,
            delimiterDisplay);

        // Convert plain text messages to Message and publish
        var plainMessagesWithProps = ConvertToMessages(messages);
        return await PublishMessageInternal(dest, plainMessagesWithProps, burstCount, cancellationToken);
    }

    public async Task<int> PublishMessageFromStdin(DestinationInfo dest, int burstCount = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading messages from STDIN");
        var messageBlob = await Console.In.ReadToEndAsync(cancellationToken);

        // Auto-detect format: Try JSON first, fallback to plain text
        try
        {
            _logger.LogDebug("Attempting to parse STDIN as JSON (NDJSON format)");
            var jsonMessages = JsonMessageParser.ParseNdjson(messageBlob);

            if (jsonMessages.Count > 0)
            {
                _logger.LogDebug("Detected JSON format: Parsed {MessageCount} JSON messages from STDIN", jsonMessages.Count);

                // Merge JSON messages with CLI options
                var mergedMessages = jsonMessages.Select(jsonMsg => PropertyMerger.Merge(jsonMsg, _options)).ToList();

                return await PublishMessageInternal(dest, mergedMessages, burstCount, cancellationToken);
            }
        }
        catch (ArgumentException)
        {
            // Not valid JSON, fallback to plain text
            _logger.LogDebug("JSON parsing failed, treating STDIN as plain text");
        }

        // Plain text mode (delimiter-separated messages)
        _logger.LogDebug("Detected plain text format");
        var (messages, delimiterDisplay) = SplitMessages(messageBlob);

        _logger.LogDebug("Read {MessageCount} messages from STDIN with delimiter '{MessageDelimiter}'", messages.Count,
            delimiterDisplay);

        // Convert plain text messages to Message and publish
        var plainMessagesWithProps = ConvertToMessages(messages);
        return await PublishMessageInternal(dest, plainMessagesWithProps, burstCount, cancellationToken);
    }

    private async Task PublishCore(
        List<Message> messages,
        IChannel channel,
        DestinationInfo dest,
        List<PublishOperationDto> results,
        IProgress<int>? progress = null,
        int burstCount = 1,
        CancellationToken cancellationToken = default)
    {
        var messageBaseId = GetMessageId();
        var currentProgress = 0;

        for (var m = 0; m < messages.Count; m++)
        {
            var message = messages[m];
            var body = Encoding.UTF8.GetBytes(message.Body);
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

                // Apply user-specified properties and headers
                ApplyPropertiesToBasicProperties(props, message.Properties, message.Headers);

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
    }

    /// <summary>
    /// Applies user-specified properties and headers to RabbitMQ BasicProperties.
    /// Only sets properties that are present (not null).
    /// </summary>
    private static void ApplyPropertiesToBasicProperties(
        BasicProperties props,
        MessageProperties? userProps,
        Dictionary<string, object>? headers)
    {
        // Apply headers
        if (headers != null && headers.Count > 0)
            props.Headers = (IDictionary<string, object?>)headers;

        if (userProps == null)
            return;

        // Apply properties

        if (userProps.AppId != null)
            props.AppId = userProps.AppId;
        if (userProps.ContentType != null)
            props.ContentType = userProps.ContentType;
        if (userProps.ContentEncoding != null)
            props.ContentEncoding = userProps.ContentEncoding;
        if (userProps.CorrelationId != null)
            props.CorrelationId = userProps.CorrelationId;
        if (userProps.DeliveryMode.HasValue)
            props.DeliveryMode = userProps.DeliveryMode.Value;
        if (userProps.Expiration != null)
            props.Expiration = userProps.Expiration;
        if (userProps.Priority.HasValue)
            props.Priority = userProps.Priority.Value;
        if (userProps.ReplyTo != null)
            props.ReplyTo = userProps.ReplyTo;
        if (userProps.Type != null)
            props.Type = userProps.Type;
        if (userProps.UserId != null)
            props.UserId = userProps.UserId;
        if (userProps.ClusterId != null)
            props.ClusterId = userProps.ClusterId;

        // TODO: Add CLI options for these properties if needed
        // if (userProps.MessageId != null)
        //     props.MessageId = userProps.MessageId;
        // if (userProps.Timestamp.HasValue)
        //     props.Timestamp = new AmqpTimestamp(userProps.Timestamp.Value);
    }

    /// <summary>
    /// Internal method to publish messages with properties and headers.
    /// </summary>
    private async Task<int> PublishMessageInternal(
        DestinationInfo dest,
        List<Message> messagesWithProps,
        int burstCount = 1,
        CancellationToken cancellationToken = default)
    {
        var startTime = Stopwatch.GetTimestamp();

        _logger.LogDebug(
            "Initiating publish operation: exchange={Exchange}, routing-key={RoutingKey}, queue={Queue}, msg-count={MessageCount}, burst-count={BurstCount}",
            dest.Exchange, dest.RoutingKey, dest.Queue, messagesWithProps.Count, burstCount);

        await using var channel = await _rabbitChannelFactory.GetChannelWithPublisherConfirmsAsync();

        var totalMessageCount = messagesWithProps.Count * burstCount;
        var messageCountString = OutputUtilities.GetMessageCountString(totalMessageCount, _statusOutput.NoColor);

        // Prepare the list to collect publish results
        var publishResults = new List<PublishOperationDto>();

        try
        {
            _statusOutput.ShowStatus($"Publishing {messageCountString} to {GetDestinationString(dest)} (Ctrl+C to stop)");

            await _statusOutput.ExecuteWithProgress(
                description: "Publishing messages",
                maxValue: totalMessageCount,
                workload: progress =>
                    PublishCore(messagesWithProps, channel, dest, publishResults, progress, burstCount, cancellationToken));

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
            var failCount = messagesWithProps.Count - successCount;
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

                var errorResult = PublishResponseFactory.Failure(dest, messagesWithProps.Count, elapsedTime);
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

    /// <summary>
    /// Converts plain text messages to Message, applying CLI properties and headers.
    /// </summary>
    private List<Message> ConvertToMessages(List<string> messages)
    {
        var (cliProps, cliHeaders) = CreateMessagePropertiesAndHeadersFromOptions();
        return messages.Select(body => new Message
        {
            Body = body,
            Properties = cliProps,
            Headers = cliHeaders
        }).ToList();
    }

    /// <summary>
    /// Creates MessageProperties and headers from CLI options.
    /// Returns null for both if no options are specified.
    /// </summary>
    private (MessageProperties? properties, Dictionary<string, object>? headers) CreateMessagePropertiesAndHeadersFromOptions()
    {
        // Check if any properties are set
        var hasProperties = _options.AppId != null ||
                            _options.ClusterId != null ||
                            _options.ContentType != null ||
                            _options.ContentEncoding != null ||
                            _options.CorrelationId != null ||
                            _options.DeliveryMode.HasValue ||
                            _options.Expiration != null ||
                            _options.Priority.HasValue ||
                            _options.ReplyTo != null ||
                            _options.Type != null ||
                            _options.UserId != null;

        var properties = hasProperties
            ? new MessageProperties
            {
                AppId = _options.AppId,
                ClusterId = _options.ClusterId,
                ContentType = _options.ContentType,
                ContentEncoding = _options.ContentEncoding,
                CorrelationId = _options.CorrelationId,
                DeliveryMode = _options.DeliveryMode,
                Expiration = _options.Expiration,
                Priority = _options.Priority,
                ReplyTo = _options.ReplyTo,
                Type = _options.Type,
                UserId = _options.UserId
            }
            : null;

        var headers = _options.Headers != null && _options.Headers.Count > 0
            ? _options.Headers
            : null;

        return (properties, headers);
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
            return $"queue {colorPrefix}{dest.Queue}{colorSuffix}";
        }

        if (!string.IsNullOrEmpty(dest.Exchange) && !string.IsNullOrEmpty(dest.RoutingKey))
        {
            return $"exchange {colorPrefix}{dest.Exchange}{colorSuffix} with routing key {colorPrefix}{dest.RoutingKey}{colorSuffix}";
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