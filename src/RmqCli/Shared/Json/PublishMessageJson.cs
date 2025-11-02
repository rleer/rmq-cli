namespace RmqCli.Shared.Json;

/// <summary>
/// Represents a message to be published, with body, properties, and headers.
/// Used for JSON input format (--message, --message-file, --json-format).
/// </summary>
public class PublishMessageJson
{
    public string Body { get; set; } = string.Empty;
    public PublishPropertiesJson? Properties { get; set; }
    public Dictionary<string, object>? Headers { get; set; }
}
