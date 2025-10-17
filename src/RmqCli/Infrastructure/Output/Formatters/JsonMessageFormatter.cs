using System.Text.Json;
using RmqCli.Commands.Consume;
using RmqCli.Shared.Json;

namespace RmqCli.Infrastructure.Output.Formatters;

public static class JsonMessageFormatter
{
    public static string FormatMessage(RabbitMessage message)
    {
        var messageJson = CreateMessageJson(message);
        return JsonSerializer.Serialize(messageJson, JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(MessageJson)));
    }

    public static string FormatMessages(IEnumerable<RabbitMessage> messages)
    {
        var messageArr = messages.Select(CreateMessageJson).ToArray();
        var wrapper = new MessageJsonArray(messageArr);
        return JsonSerializer.Serialize(wrapper, JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(MessageJsonArray)));
    }

    private static MessageJson CreateMessageJson(RabbitMessage message)
    {
        var formattedProps = MessagePropertyExtractor.ExtractProperties(message.Props);
        var properties = ConvertToJsonProperties(formattedProps);

        return new MessageJson(
            message.Exchange,
            message.RoutingKey,
            message.DeliveryTag,
            message.Redelivered,
            message.Body,
            properties
        );
    }

    /// <summary>
    /// Converts FormattedMessageProperties to a dictionary suitable for JSON serialization.
    /// Only includes properties that are present (not null).
    /// </summary>
    private static Dictionary<string, object>? ConvertToJsonProperties(FormattedMessageProperties props)
    {
        if (!props.HasAnyProperty())
        {
            return null;
        }

        var properties = new Dictionary<string, object>();

        if (props.Type != null)
            properties["type"] = props.Type;
        if (props.MessageId != null)
            properties["messageId"] = props.MessageId;
        if (props.AppId != null)
            properties["appId"] = props.AppId;
        if (props.ClusterId != null)
            properties["clusterId"] = props.ClusterId;
        if (props.ContentType != null)
            properties["contentType"] = props.ContentType;
        if (props.ContentEncoding != null)
            properties["contentEncoding"] = props.ContentEncoding;
        if (props.CorrelationId != null)
            properties["correlationId"] = props.CorrelationId;
        if (props.DeliveryMode != null)
            properties["deliveryMode"] = props.DeliveryMode.Value;
        if (props.Expiration != null)
            properties["expiration"] = props.Expiration;
        if (props.Priority != null)
            properties["priority"] = props.Priority.Value;
        if (props.ReplyTo != null)
            properties["replyTo"] = props.ReplyTo;
        if (props.Timestamp != null)
            properties["timestamp"] = props.Timestamp;
        if (props.Headers != null)
            properties["headers"] = props.Headers;

        return properties.Count > 0 ? properties : null;
    }
}