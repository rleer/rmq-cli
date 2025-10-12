using System.Text;
using RabbitMQ.Client;

namespace RmqCli.ConsumeCommand.MessageFormatter;

/// <summary>
/// Extracts and converts RabbitMQ message properties into a common format
/// that can be used by different formatters (text, JSON, etc.).
/// </summary>
public static class MessagePropertyExtractor
{
    /// <summary>
    /// Extracts all properties from IReadOnlyBasicProperties into a formatted representation.
    /// </summary>
    public static FormattedMessageProperties ExtractProperties(IReadOnlyBasicProperties? props)
    {
        if (props == null)
        {
            return new FormattedMessageProperties();
        }

        // TODO: Make choice of properties configurable.
        return new FormattedMessageProperties
        {
            Type = props.IsTypePresent() ? props.Type : null,
            MessageId = props.IsMessageIdPresent() ? props.MessageId : null,
            AppId = props.IsAppIdPresent() ? props.AppId : null,
            ClusterId = props.IsClusterIdPresent() ? props.ClusterId : null,
            ContentType = props.IsContentTypePresent() ? props.ContentType : null,
            ContentEncoding = props.IsContentEncodingPresent() ? props.ContentEncoding : null,
            CorrelationId = props.IsCorrelationIdPresent() ? props.CorrelationId : null,
            DeliveryMode = props.IsDeliveryModePresent() ? props.DeliveryMode : null,
            Expiration = props.IsExpirationPresent() ? props.Expiration : null,
            Priority = props.IsPriorityPresent() ? props.Priority : null,
            ReplyTo = props.IsReplyToPresent() ? props.ReplyTo : null,
            Timestamp = props.IsTimestampPresent()
                ? FormatTimestamp(props.Timestamp)
                : null,
            Headers = props.IsHeadersPresent() && props.Headers != null
                ? ConvertHeaders(props.Headers)
                : null
        };
    }

    /// <summary>
    /// Converts RabbitMQ headers dictionary, handling special types like byte arrays and timestamps.
    /// </summary>
    private static Dictionary<string, object>? ConvertHeaders(IDictionary<string, object?> headers)
    {
        var convertedHeaders = new Dictionary<string, object>();

        foreach (var header in headers)
        {
            if (header.Value != null)
            {
                convertedHeaders[header.Key] = ConvertValue(header.Value);
            }
        }

        return convertedHeaders.Count > 0 ? convertedHeaders : null;
    }

    /// <summary>
    /// Recursively converts header values to appropriate types for formatting.
    /// Handles byte arrays, timestamps, collections, and nested dictionaries.
    /// </summary>
    public static object ConvertValue(object value)
    {
        return value switch
        {
            null => "null",
            byte[] bytes => ConvertByteArray(bytes),
            AmqpTimestamp timestamp => FormatTimestamp(timestamp),
            IEnumerable<object> enumerable when value is not string => enumerable.Select(ConvertValue).ToArray(),
            IDictionary<string, object> dict => dict.ToDictionary(pair => pair.Key, pair => ConvertValue(pair.Value)),
            _ => value
        };
    }

    /// <summary>
    /// Converts byte array to string if possible, otherwise returns a description of binary data.
    /// </summary>
    private static object ConvertByteArray(byte[] bytes)
    {
        try
        {
            var strValue = Encoding.UTF8.GetString(bytes);
            // Check if the string contains control characters (except common ones)
            if (strValue.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
            {
                return FormatBinaryData(bytes.Length);
            }

            return strValue;
        }
        catch
        {
            return FormatBinaryData(bytes.Length);
        }
    }

    /// <summary>
    /// Formats binary data description. Can be customized per output format.
    /// </summary>
    public static string FormatBinaryData(int length)
    {
        return $"<binary data: {length} bytes>";
    }

    /// <summary>
    /// Formats an AMQP timestamp to a standard string format.
    /// </summary>
    private static string FormatTimestamp(AmqpTimestamp timestamp)
    {
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixTime);
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
