using System.Text.Json.Serialization;

namespace RmqCli.Commands.Publish;

public class PublishResult
{
    [JsonPropertyName("messages_published")]
    public int MessagesPublished { get; set; }

    [JsonPropertyName("messages_failed")]
    public int MessagesFailed { get; set; }

    [JsonPropertyName("duration_ms")]
    public double DurationMs { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonPropertyName("message_ids")]
    public List<string> MessageIds { get; set; } = new();

    [JsonPropertyName("first_message_id")]
    public string? FirstMessageId { get; set; } = string.Empty;

    [JsonPropertyName("last_message_id")]
    public string? LastMessageId { get; set; } = string.Empty;
    
    [JsonPropertyName("first_timestamp")]
    public string? FirstTimestamp { get; set; } = string.Empty;
    
    [JsonPropertyName("last_timestamp")]
    public string? LastTimestamp { get; set; } = string.Empty;
    
    [JsonPropertyName("average_size_bytes")]
    public double AverageMessageSizeBytes { get; set; }

    [JsonPropertyName("average_size")]
    public string AverageMessageSize { get; set; } = string.Empty;

    [JsonPropertyName("total_size_bytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("total_size")]
    public string TotalSize { get; set; } = string.Empty;

    [JsonPropertyName("messages_per_second")]
    public double MessagesPerSecond { get; set; }
}