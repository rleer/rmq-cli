using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RmqCli.Core.Models;
using RmqCli.Core.Services;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output;
using RmqCli.Shared;
using RmqCli.Shared.Factories;
using PublishErrorInfoFactory = RmqCli.Shared.Factories.PublishErrorInfoFactory;

namespace RmqCli.Commands.Publish;

/// <summary>
/// Represents message properties that can be set by the user.
/// </summary>
public class MessageProperties
{
    public string? AppId { get; init; }
    public string? ContentType { get; init; }
    public string? ContentEncoding { get; init; }
    public string? CorrelationId { get; init; }
    public DeliveryModes? DeliveryMode { get; init; }
    public string? Expiration { get; init; }
    public string? MessageId { get; init; }
    public byte? Priority { get; init; }
    public string? ReplyTo { get; init; }
    public long? Timestamp { get; init; }
    public string? Type { get; init; }
    public string? UserId { get; init; }
    public Dictionary<string, object>? Headers { get; init; }
}

/// <summary>
/// Represents a message with optional properties to be published.
/// </summary>
public class MessageWithProperties
{
    public string Body { get; init; } = string.Empty;
    public MessageProperties? Properties { get; init; }
}

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
    /// <param name="messages"></param>
    /// <param name="burstCount"></param>
    /// <param name="cancellationToken"></param>
    public async Task<int> PublishMessage(
        DestinationInfo dest,
        List<string> messages,
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

                // Convert JSON message to MessageWithProperties, merging with CLI options
                var (mergedProps, mergedHeaders) = PropertyMerger.Merge(jsonMessage, _options);
                var messageWithProps = new MessageWithProperties
                {
                    Body = jsonMessage.Body,
                    Properties = mergedProps != null ? ConvertToMessageProperties(mergedProps, mergedHeaders) : null
                };

                return await PublishMessageInternal(dest, [messageWithProps], burstCount, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Failed to parse inline JSON message");
                _statusOutput.ShowError($"Failed to parse inline JSON message: {ex.Message}");
                return 1;
            }
        }

        // Convert plain text messages to MessageWithProperties
        var messagesWithProps = ConvertToMessagesWithProperties(messages);
        return await PublishMessageInternal(dest, messagesWithProps, burstCount, cancellationToken);
    }

    public async Task<int> PublishMessageFromFile(DestinationInfo dest, FileInfo fileInfo, int burstCount = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading messages from file: {FilePath}", fileInfo.FullName);
        var messageBlob = await File.ReadAllTextAsync(fileInfo.FullName, cancellationToken);

        // Check if this is a JSON message file
        if (_options.JsonMessageFile != null)
        {
            _logger.LogDebug("Parsing JSON messages from file: {FilePath}", fileInfo.FullName);

            try
            {
                var jsonMessages = JsonMessageParser.ParseNdjson(messageBlob);
                _logger.LogDebug("Parsed {MessageCount} JSON messages from '{FilePath}'", jsonMessages.Count, fileInfo.FullName);

                // Convert JSON messages to MessageWithProperties, merging with CLI options
                var messagesWithProps = jsonMessages.Select(jsonMsg =>
                {
                    var (mergedProps, mergedHeaders) = PropertyMerger.Merge(jsonMsg, _options);
                    return new MessageWithProperties
                    {
                        Body = jsonMsg.Body,
                        Properties = mergedProps != null ? ConvertToMessageProperties(mergedProps, mergedHeaders) : null
                    };
                }).ToList();

                return await PublishMessageInternal(dest, messagesWithProps, burstCount, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON messages from file: {FilePath}", fileInfo.FullName);
                _statusOutput.ShowError($"Failed to parse JSON messages from file: {ex.Message}");
                return 1;
            }
        }

        // Plain text mode
        var (messages, delimiterDisplay) = SplitMessages(messageBlob);

        _logger.LogDebug("Read {MessageCount} messages from '{FilePath}' with delimiter '{MessageDelimiter}'", messages.Count, fileInfo.FullName,
            delimiterDisplay);

        return await PublishMessage(dest, messages, burstCount, cancellationToken);
    }

    public async Task<int> PublishMessageFromStdin(DestinationInfo dest, int burstCount = 1, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading messages from STDIN");
        var messageBlob = await Console.In.ReadToEndAsync(cancellationToken);

        // Check if this is JSON format mode
        if (_options.UseJsonFormat)
        {
            _logger.LogDebug("Parsing JSON messages from STDIN");

            try
            {
                var jsonMessages = JsonMessageParser.ParseNdjson(messageBlob);
                _logger.LogDebug("Parsed {MessageCount} JSON messages from STDIN", jsonMessages.Count);

                // Convert JSON messages to MessageWithProperties, merging with CLI options
                var messagesWithProps = jsonMessages.Select(jsonMsg =>
                {
                    var (mergedProps, mergedHeaders) = PropertyMerger.Merge(jsonMsg, _options);
                    return new MessageWithProperties
                    {
                        Body = jsonMsg.Body,
                        Properties = mergedProps != null ? ConvertToMessageProperties(mergedProps, mergedHeaders) : null
                    };
                }).ToList();

                return await PublishMessageInternal(dest, messagesWithProps, burstCount, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON messages from STDIN");
                _statusOutput.ShowError($"Failed to parse JSON messages from STDIN: {ex.Message}");
                return 1;
            }
        }

        // Plain text mode
        var (messages, delimiterDisplay) = SplitMessages(messageBlob);

        _logger.LogDebug("Read {MessageCount} messages from STDIN with delimiter '{MessageDelimiter}'", messages.Count,
            delimiterDisplay);

        return await PublishMessage(dest, messages, burstCount, cancellationToken);
    }

    private async Task PublishCore(
        List<MessageWithProperties> messages,
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

                // Apply user-specified properties (may override MessageId and Timestamp)
                ApplyPropertiesToBasicProperties(props, message.Properties);

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
    /// Applies user-specified properties to RabbitMQ BasicProperties.
    /// Only sets properties that are present (not null) in userProps.
    /// </summary>
    private static void ApplyPropertiesToBasicProperties(
        BasicProperties props,
        MessageProperties? userProps)
    {
        if (userProps == null)
            return;

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
        if (userProps.Headers != null && userProps.Headers.Count > 0)
            props.Headers = (IDictionary<string, object?>)userProps.Headers;

        // Override MessageId and Timestamp only if provided by user
        if (userProps.MessageId != null)
            props.MessageId = userProps.MessageId;
        if (userProps.Timestamp.HasValue)
            props.Timestamp = new AmqpTimestamp(userProps.Timestamp.Value);
    }

    /// <summary>
    /// Internal method to publish messages with properties.
    /// </summary>
    private async Task<int> PublishMessageInternal(
        DestinationInfo dest,
        List<MessageWithProperties> messagesWithProps,
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
            _statusOutput.ShowStatus($"Publishing {messageCountString} to {GetDestinationString(dest)}...");

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
    /// Converts plain text messages to MessageWithProperties, applying CLI properties.
    /// </summary>
    private List<MessageWithProperties> ConvertToMessagesWithProperties(List<string> messages)
    {
        var cliProps = CreateMessagePropertiesFromOptions();
        return messages.Select(body => new MessageWithProperties
        {
            Body = body,
            Properties = cliProps
        }).ToList();
    }

    /// <summary>
    /// Converts PublishPropertiesJson and headers to MessageProperties.
    /// </summary>
    private static MessageProperties ConvertToMessageProperties(
        RmqCli.Shared.Json.PublishPropertiesJson props,
        Dictionary<string, object>? headers)
    {
        return new MessageProperties
        {
            AppId = props.AppId,
            ContentType = props.ContentType,
            ContentEncoding = props.ContentEncoding,
            CorrelationId = props.CorrelationId,
            DeliveryMode = props.DeliveryMode,
            Expiration = props.Expiration,
            MessageId = props.MessageId,
            Priority = props.Priority,
            ReplyTo = props.ReplyTo,
            Timestamp = props.Timestamp,
            Type = props.Type,
            UserId = props.UserId,
            Headers = headers
        };
    }

    /// <summary>
    /// Creates MessageProperties from CLI options.
    /// Returns null if no properties are specified.
    /// </summary>
    private MessageProperties? CreateMessagePropertiesFromOptions()
    {
        // Check if any properties are set
        if (_options.AppId == null &&
            _options.ContentType == null &&
            _options.ContentEncoding == null &&
            _options.CorrelationId == null &&
            !_options.DeliveryMode.HasValue &&
            _options.Expiration == null &&
            !_options.Priority.HasValue &&
            _options.ReplyTo == null &&
            _options.Type == null &&
            _options.UserId == null &&
            (_options.Headers == null || _options.Headers.Count == 0))
        {
            return null;
        }

        return new MessageProperties
        {
            AppId = _options.AppId,
            ContentType = _options.ContentType,
            ContentEncoding = _options.ContentEncoding,
            CorrelationId = _options.CorrelationId,
            DeliveryMode = _options.DeliveryMode,
            Expiration = _options.Expiration,
            Priority = _options.Priority,
            ReplyTo = _options.ReplyTo,
            Type = _options.Type,
            UserId = _options.UserId,
            Headers = _options.Headers
        };
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