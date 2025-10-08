using System.Text.RegularExpressions;
using RmqCli.Common;

namespace RmqCli.PublishCommand;

public static partial class PublishErrorInfoFactory
{
    public static ErrorInfo NoRouteErrorInfo(bool isQueue)   
    {
        return new ErrorInfo
        {
            Category = "routing",
            Code = "NO_ROUTE",
            Error = "No route to destination",
            Suggestion = isQueue ? "Check if the queue exists" : "Check if the exchange and routing key exist"
        };
    }
    
    public static ErrorInfo ExchangeNotFoundErrorInfo()
    {
        return new ErrorInfo
        {
            Category = "routing",
            Code = "EXCHANGE_NOT_FOUND",
            Error = "Exchange not found",
            Suggestion = "Check if the exchange exists and is correctly configured"
        };
    }
    
    public static ErrorInfo MaxSizeExceededErrorInfo(string errorText)
    {
        var regex = MaxMessageSizeRegex().Match(errorText);
        var messageSize = regex.Success ? regex.Groups["message_size"].Value : null;
        var maxSize = regex.Success ? regex.Groups["max_size"].Value : null;
        var messageSizeValue = long.TryParse(messageSize, out var size) ? size : 0;
        var maxSizeValue = long.TryParse(maxSize, out var max) ? max : 0;
        var messageSizeString = messageSizeValue > 0 ? OutputUtilities.ToSizeString(messageSizeValue) + " " : string.Empty;
        var maxSizeString = maxSizeValue > 0 ? " " + OutputUtilities.ToSizeString(maxSizeValue) : string.Empty;
        
        return new ErrorInfo
        {
            Category = "validation",
            Code = "MESSAGE_SIZE_EXCEEDED",
            Error = $"Message size {messageSizeString}exceeds maximum allowed size{maxSizeString}",
            Suggestion = $"Reduce the message size to {maxSize} bytes or less, or adapt RabbitMQ configuration",
            Details = new Dictionary<string, object>
            {
                { "max_size", maxSizeString },
                { "message_size", messageSizeString }
            }
        };
    }
    
    [GeneratedRegex(@"message size (?<message_size>\d+).+max size (?<max_size>\d+)$")]
    private static partial Regex MaxMessageSizeRegex();
}