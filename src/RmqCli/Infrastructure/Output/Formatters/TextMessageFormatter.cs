using System.Text;
using RabbitMQ.Client;
using RmqCli.Commands.Consume;

namespace RmqCli.Infrastructure.Output.Formatters;

public static class TextMessageFormatter
{
    public static string FormatMessage(RabbitMessage message)
    {
        return "DeliveryTag: " + message.DeliveryTag + "\n" +
               "Exchange: " + message.Exchange + "\n" +
               "RoutingKey: " + message.RoutingKey + "\n" +
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
            sb.AppendLine($"  {header.Key}: {HeaderValueFormatter.FormatValue(header.Value)}");
        }

        return sb.ToString().TrimEnd();
    }
}