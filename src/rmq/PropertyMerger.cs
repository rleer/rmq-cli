namespace Rmq;

/// <summary>
/// Merges a message read from JSON with the properties given on the command line.
/// CLI options win, per field. Takes properties rather than a PublishOptions type so
/// it stays a pure function the unit slice can exercise without a command in scope.
/// </summary>
public static class PropertyMerger
{
    public static Message Merge(Message message, MessageProperties? cli, Dictionary<string, object>? cliHeaders)
    {
        if (cli == null && (cliHeaders == null || cliHeaders.Count == 0))
        {
            return message;
        }

        var json = message.Properties ?? new MessageProperties();

        var headers = json.Headers != null ? new Dictionary<string, object>(json.Headers) : null;
        if (cliHeaders is { Count: > 0 })
        {
            headers ??= new Dictionary<string, object>();
            foreach (var (key, value) in cliHeaders)
            {
                headers[key] = value;
            }
        }

        return message with
        {
            Properties = new MessageProperties
            {
                ContentType = cli?.ContentType ?? json.ContentType,
                ContentEncoding = cli?.ContentEncoding ?? json.ContentEncoding,
                DeliveryMode = cli?.DeliveryMode ?? json.DeliveryMode,
                Priority = cli?.Priority ?? json.Priority,
                CorrelationId = cli?.CorrelationId ?? json.CorrelationId,
                ReplyTo = cli?.ReplyTo ?? json.ReplyTo,
                Expiration = cli?.Expiration ?? json.Expiration,
                MessageId = cli?.MessageId ?? json.MessageId,
                Timestamp = cli?.Timestamp ?? json.Timestamp,
                Type = cli?.Type ?? json.Type,
                UserId = cli?.UserId ?? json.UserId,
                AppId = cli?.AppId ?? json.AppId,
                Headers = headers
            }
        };
    }
}
