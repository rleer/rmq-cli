using System.Text;
using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Shared.Output.Formatters;

/// <summary>
/// Extracts and converts RabbitMQ message properties into a common format
/// that can be used by different formatters (text, JSON, etc.).
/// </summary>
public static class MessagePropertyExtractor
{
    /// <summary>
    /// Extracts properties and headers from IReadOnlyBasicProperties.
    /// Returns a tuple with MessageProperties (standard AMQP properties) and Headers (custom headers).
    /// Timestamp is extracted as Unix seconds (long).
    /// </summary>
    public static (MessageProperties properties, Dictionary<string, object>? headers) ExtractPropertiesAndHeaders(IReadOnlyBasicProperties? props)
    {
        if (props == null)
        {
            return (new MessageProperties(), null);
        }

        var properties = new MessageProperties
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
            UserId = props.IsUserIdPresent() ? props.UserId : null,
            Timestamp = props.IsTimestampPresent()
                ? props.Timestamp.UnixTime
                : null
        };

        var headers = props.IsHeadersPresent() && props.Headers != null
            ? ConvertHeaders(props.Headers)
            : null;

        return (properties, headers);
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
    private static object ConvertValue(object value)
    {
        return value switch
        {
            null => "null",
            byte[] bytes => ConvertByteArray(bytes),
            AmqpTimestamp timestamp => timestamp.UnixTime,
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
    private static string FormatBinaryData(int length)
    {
        return $"<binary data: {length} bytes>";
    }
}
