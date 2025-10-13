using System.Text;
using RabbitMQ.Client;

namespace RmqCli.ConsumeCommand.MessageFormatter;

public static class TextMessageFormatter
{
    public static string FormatMessage(RabbitMessage message)
    {
        return "DeliveryTag: " + message.DeliveryTag + "\n" +
               "Redelivered: " + message.Redelivered + "\n" +
               FormatBasicProperties(message.Props) +
               "Body:\n" + message.Body;
    }

    public static string FormatMessages(IEnumerable<RabbitMessage> messages)
    {
        return string.Join("\n", messages.Select(FormatMessage));
    }

    private static string FormatBasicProperties(FormattedMessageProperties props)
    {
        if (!props.HasAnyProperty())
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        if (props.Type != null)
            sb.AppendLine($"Type: {props.Type}");
        if (props.MessageId != null)
            sb.AppendLine($"MessageId: {props.MessageId}");
        if (props.AppId != null)
            sb.AppendLine($"AppId: {props.AppId}");
        if (props.ClusterId != null)
            sb.AppendLine($"ClusterId: {props.ClusterId}");
        if (props.ContentType != null)
            sb.AppendLine($"ContentType: {props.ContentType}");
        if (props.ContentEncoding != null)
            sb.AppendLine($"ContentEncoding: {props.ContentEncoding}");
        if (props.CorrelationId != null)
            sb.AppendLine($"CorrelationId: {props.CorrelationId}");
        if (props.DeliveryMode != null)
            sb.AppendLine($"DeliveryMode: {props.DeliveryMode}");
        if (props.Expiration != null)
            sb.AppendLine($"Expiration: {props.Expiration}");
        if (props.Priority != null)
            sb.AppendLine($"Priority: {props.Priority}");
        if (props.ReplyTo != null)
            sb.AppendLine($"ReplyTo: {props.ReplyTo}");
        if (props.Timestamp != null)
            sb.AppendLine($"Timestamp: {props.Timestamp}");

        // Format headers if present
        if (props.Headers != null)
            sb.AppendLine(FormatHeaders(props.Headers));

        return sb.ToString();
    }

    private static string FormatBasicProperties(IReadOnlyBasicProperties? messageProps)
    {
        var formattedProps = MessagePropertyExtractor.ExtractProperties(messageProps);
        return FormatBasicProperties(formattedProps);
    }

    private static string FormatHeaders(IDictionary<string, object> headers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Headers:");
        foreach (var header in headers)
        {
            sb.AppendLine($"  {header.Key}: {FormatValue(header.Value)}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string FormatValue(object value, int indentationLevel = 1)
    {
        switch (value)
        {
            case null:
                return "null";
            case string str when str.StartsWith("<binary data:"):
                // Binary data already formatted by extractor, just need to adjust format
                return str.Replace("<binary data: ", "byte[").Replace(" bytes>", "]");
            case IEnumerable<object> enumerable when value is not string:
            {
                var sb = new StringBuilder();
                sb.AppendLine("[");
                foreach (var item in enumerable)
                {
                    sb.AppendLine($"{new string(' ', (indentationLevel + 1) * 2 - 2)}- {FormatValue(item, indentationLevel + 1).TrimStart()}");
                }
                sb.AppendLine($"{new string(' ', indentationLevel)} ]");
                return sb.ToString().TrimEnd();
            }
            case IDictionary<string, object> dict:
            {
                var sb = new StringBuilder();
                foreach (var pair in dict)
                {
                    sb.AppendLine($"{new string(' ', indentationLevel * 2)}{pair.Key}: {FormatValue(pair.Value, indentationLevel + 1)}");
                }

                return sb.ToString().TrimEnd();
            }
            default:
                return value.ToString() ?? "null";
        }
    }
}