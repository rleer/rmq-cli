using RmqCli.Shared.Json;

namespace RmqCli.Commands.Publish;

/// <summary>
/// Merges JSON properties/headers with CLI options. CLI options take precedence.
/// </summary>
public static class PropertyMerger
{
    /// <summary>
    /// Merges JSON properties/headers with CLI options. CLI options take precedence.
    /// </summary>
    public static (PublishPropertiesJson? properties, Dictionary<string, object>? headers) Merge(
        PublishMessageJson jsonMessage,
        PublishOptions cliOptions)
    {
        // Start with copies of JSON properties and headers
        var mergedProps = jsonMessage.Properties != null
            ? new PublishPropertiesJson
            {
                AppId = jsonMessage.Properties.AppId,
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
            : new PublishPropertiesJson();

        var mergedHeaders = jsonMessage.Headers != null
            ? new Dictionary<string, object>(jsonMessage.Headers)
            : null;

        // Override properties with CLI options (if specified)
        if (cliOptions.AppId != null)
            mergedProps.AppId = cliOptions.AppId;
        if (cliOptions.ContentType != null)
            mergedProps.ContentType = cliOptions.ContentType;
        if (cliOptions.ContentEncoding != null)
            mergedProps.ContentEncoding = cliOptions.ContentEncoding;
        if (cliOptions.CorrelationId != null)
            mergedProps.CorrelationId = cliOptions.CorrelationId;
        if (cliOptions.DeliveryMode.HasValue)
            mergedProps.DeliveryMode = cliOptions.DeliveryMode;
        if (cliOptions.Expiration != null)
            mergedProps.Expiration = cliOptions.Expiration;
        if (cliOptions.Priority.HasValue)
            mergedProps.Priority = cliOptions.Priority;
        if (cliOptions.ReplyTo != null)
            mergedProps.ReplyTo = cliOptions.ReplyTo;
        if (cliOptions.Type != null)
            mergedProps.Type = cliOptions.Type;
        if (cliOptions.UserId != null)
            mergedProps.UserId = cliOptions.UserId;

        // Merge headers (CLI headers supplement/override JSON headers)
        if (cliOptions.Headers != null && cliOptions.Headers.Count > 0)
        {
            mergedHeaders ??= new Dictionary<string, object>();
            foreach (var (key, value) in cliOptions.Headers)
            {
                mergedHeaders[key] = value;
            }
        }

        return (mergedProps, mergedHeaders);
    }
}
