using System.Text;
using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Shared.Output.Formatters;

/// <summary>
/// Formats RabbitMQ messages as plain text with key-value pairs.
/// </summary>
public static class TextMessageFormatter
{
    /// <summary>
    /// Formats a single message as plain text.
    /// </summary>
    /// <param name="message">The message to format</param>
    /// <param name="compact">If true, only show properties with values. If false, show all properties with "-" for empty values.</param>
    public static string FormatMessage(RetrievedMessage message, bool compact = false)
    {
        var sb = new StringBuilder();

        // Routing Information
        sb.AppendLine($"== Message #{message.DeliveryTag} ==");
        sb.AppendLine($"Queue: {message.Queue}");
        sb.AppendLine($"Routing Key: {message.RoutingKey}");
        sb.AppendLine($"Exchange: {(string.IsNullOrEmpty(message.Exchange) ? "-" : message.Exchange)}");
        sb.AppendLine($"Redelivered: {(message.Redelivered ? "Yes" : "No")}");

        // Properties
        if (message.Properties != null && (message.Properties.HasAnyProperty() || !compact))
        {
            sb.AppendLine("== Properties ==");
            sb.Append(FormatProperties(message.Properties, compact));
        }

        // Custom Headers
        if (message.Headers != null && message.Headers.Count > 0)
        {
            sb.AppendLine("== Custom Headers ==");
            sb.Append(FormatHeaders(message.Headers));
        }

        // Body
        sb.AppendLine($"== Body ({message.BodySize}) ==");
        sb.Append(message.Body);

        return sb.ToString();
    }

    /// <summary>
    /// Formats multiple messages separated by newlines.
    /// </summary>
    public static string FormatMessages(IEnumerable<RetrievedMessage> messages, bool compact = false)
    {
        var messageList = messages.ToList();
        var sb = new StringBuilder();

        for (int i = 0; i < messageList.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
                sb.AppendLine(); // Blank line between messages
            }
            sb.Append(FormatMessage(messageList[i], compact));
        }

        return sb.ToString();
    }

    private static string FormatProperties(MessageProperties props, bool compact)
    {
        var sb = new StringBuilder();

        if (compact)
        {
            // Compact mode: only show properties with values
            if (props.MessageId != null)
                sb.AppendLine($"Message ID: {props.MessageId}");
            if (props.CorrelationId != null)
                sb.AppendLine($"Correlation ID: {props.CorrelationId}");
            if (props.Timestamp != null)
                sb.AppendLine($"Timestamp: {FormatTimestamp(props.Timestamp.Value)} UTC");
            if (props.ContentType != null)
                sb.AppendLine($"Content Type: {props.ContentType}");
            if (props.ContentEncoding != null)
                sb.AppendLine($"Content Encoding: {props.ContentEncoding}");
            if (props.DeliveryMode != null)
                sb.AppendLine($"Delivery Mode: {FormatDeliveryMode(props.DeliveryMode.Value)}");
            if (props.Priority != null)
                sb.AppendLine($"Priority: {props.Priority}");
            if (props.Expiration != null)
                sb.AppendLine($"Expiration: {props.Expiration}");
            if (props.ReplyTo != null)
                sb.AppendLine($"Reply To: {props.ReplyTo}");
            if (props.Type != null)
                sb.AppendLine($"Type: {props.Type}");
            if (props.AppId != null)
                sb.AppendLine($"App ID: {props.AppId}");
            if (props.UserId != null)
                sb.AppendLine($"User ID: {props.UserId}");
            if (props.ClusterId != null)
                sb.AppendLine($"Cluster ID: {props.ClusterId}");
        }
        else
        {
            // Full mode: show all properties with "-" for empty
            sb.AppendLine($"Message ID: {props.MessageId ?? "-"}");
            sb.AppendLine($"Correlation ID: {props.CorrelationId ?? "-"}");
            sb.AppendLine($"Timestamp: {(props.Timestamp != null ? FormatTimestamp(props.Timestamp.Value) + " UTC" : "-")}");
            sb.AppendLine($"Content Type: {props.ContentType ?? "-"}");
            sb.AppendLine($"Content Encoding: {props.ContentEncoding ?? "-"}");
            sb.AppendLine($"Delivery Mode: {(props.DeliveryMode != null ? FormatDeliveryMode(props.DeliveryMode.Value) : "-")}");
            sb.AppendLine($"Priority: {props.Priority?.ToString() ?? "-"}");
            sb.AppendLine($"Expiration: {props.Expiration ?? "-"}");
            sb.AppendLine($"Reply To: {props.ReplyTo ?? "-"}");
            sb.AppendLine($"Type: {props.Type ?? "-"}");
            sb.AppendLine($"App ID: {props.AppId ?? "-"}");
            sb.AppendLine($"User ID: {props.UserId ?? "-"}");
            sb.AppendLine($"Cluster ID: {props.ClusterId ?? "-"}");
        }

        return sb.ToString();
    }

    private static string FormatHeaders(IDictionary<string, object> headers)
    {
        var sb = new StringBuilder();
        foreach (var header in headers)
        {
            sb.AppendLine($"{header.Key}: {HeaderValueFormatter.FormatValue(header.Value)}");
        }

        return sb.ToString();
    }

    private static string FormatDeliveryMode(DeliveryModes mode)
    {
        return mode switch
        {
            DeliveryModes.Transient => "Non-persistent (1)",
            DeliveryModes.Persistent => "Persistent (2)",
            _ => mode.ToString()
        };
    }

    /// <summary>
    /// Formats a Unix timestamp (seconds) to a standard string format.
    /// </summary>
    private static string FormatTimestamp(long unixSeconds)
    {
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}