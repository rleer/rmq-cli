using System.Text.Json;
using RmqCli.Core.Models;
using RmqCli.Shared.Json;

namespace RmqCli.Infrastructure.Output.Formatters;

/// <summary>
/// Formats retrieved messages as JSON using source-generated serialization.
/// </summary>
public static class JsonMessageFormatter
{
    public static string FormatMessage(RetrievedMessage message)
    {
        return JsonSerializer.Serialize(message, JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(RetrievedMessage)));
    }

    public static string FormatMessages(IEnumerable<RetrievedMessage> messages)
    {
        var messageArr = messages.ToArray();
        return JsonSerializer.Serialize(messageArr, JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(RetrievedMessage[])));
    }
}
