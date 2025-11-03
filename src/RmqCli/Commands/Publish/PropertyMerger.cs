using RmqCli.Core.Models;

namespace RmqCli.Commands.Publish;

/// <summary>
/// Merges JSON message (properties + headers) with CLI options. CLI options take precedence.
/// </summary>
public static class PropertyMerger
{
    /// <summary>
    /// Merges JSON message with CLI options. CLI options take precedence.
    /// Returns a merged Message with properties and headers at root level.
    /// </summary>
    public static Message Merge(
        Message jsonMessage,
        PublishOptions cliOptions)
    {
        // Start with copies of JSON properties
        var mergedProps = jsonMessage.Properties != null
            ? new MessageProperties
            {
                AppId = jsonMessage.Properties.AppId,
                ClusterId = jsonMessage.Properties.ClusterId,
                ContentType = jsonMessage.Properties.ContentType,
                ContentEncoding = jsonMessage.Properties.ContentEncoding,
                CorrelationId = jsonMessage.Properties.CorrelationId,
                DeliveryMode = jsonMessage.Properties.DeliveryMode,
                Expiration = jsonMessage.Properties.Expiration,
                MessageId = jsonMessage.Properties.MessageId,
                Priority = jsonMessage.Properties.Priority,
                ReplyTo = jsonMessage.Properties.ReplyTo,
                Timestamp = jsonMessage.Properties.Timestamp,
                Type = jsonMessage.Properties.Type,
                UserId = jsonMessage.Properties.UserId
            }
            : new MessageProperties();

        // Override properties with CLI options (if specified)
        mergedProps = mergedProps with
        {
            AppId = cliOptions.AppId ?? mergedProps.AppId,
            ClusterId = cliOptions.ClusterId ?? mergedProps.ClusterId,
            ContentType = cliOptions.ContentType ?? mergedProps.ContentType,
            ContentEncoding = cliOptions.ContentEncoding ?? mergedProps.ContentEncoding,
            CorrelationId = cliOptions.CorrelationId ?? mergedProps.CorrelationId,
            DeliveryMode = cliOptions.DeliveryMode ?? mergedProps.DeliveryMode,
            Expiration = cliOptions.Expiration ?? mergedProps.Expiration,
            Priority = cliOptions.Priority ?? mergedProps.Priority,
            ReplyTo = cliOptions.ReplyTo ?? mergedProps.ReplyTo,
            Type = cliOptions.Type ?? mergedProps.Type,
            UserId = cliOptions.UserId ?? mergedProps.UserId
        };

        // Merge headers at root level (CLI headers supplement/override JSON headers)
        var mergedHeaders = jsonMessage.Headers != null
            ? new Dictionary<string, object>(jsonMessage.Headers)
            : null;

        if (cliOptions.Headers != null && cliOptions.Headers.Count > 0)
        {
            mergedHeaders ??= new Dictionary<string, object>();
            foreach (var (key, value) in cliOptions.Headers)
            {
                mergedHeaders[key] = value;
            }
        }

        return jsonMessage with
        {
            Properties = mergedProps,
            Headers = mergedHeaders
        };
    }
}
