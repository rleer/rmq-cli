using System.Text.Json.Serialization;
using RmqCli.Core.Models;

namespace RmqCli.Commands.MessageRetrieval;

public class MessageRetrievalResponse : Response
{
    [JsonPropertyName("result")]
    public MessageRetrievalResult? Result { get; set; }
    
    [JsonPropertyName("queue")]
    public string Queue { get; set; } = string.Empty;
}

public class MessageRetrievalResult
{
    [JsonPropertyName("messages_received")]
    public long MessagesReceived { get; set; }

    [JsonPropertyName("messages_processed")]
    public long MessagesProcessed { get; set; }

    [JsonPropertyName("messages_skipped")]
    public long MessagesSkipped => MessagesReceived - MessagesProcessed;

    [JsonPropertyName("duration_ms")]
    public double DurationMs { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonPropertyName("ack_mode")]
    public string AckMode { get; set; } = string.Empty;
    
    [JsonPropertyName("retrieval_mode")]
    public string RetrievalMode { get; set; } = string.Empty; // e.g., "subscribe", "polling"

    [JsonPropertyName("cancellation_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CancellationReason { get; set; }

    [JsonPropertyName("messages_per_second")]
    public double MessagesPerSecond { get; set; }

    [JsonPropertyName("total_size_bytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("total_size")]
    public string TotalSize { get; set; } = string.Empty; 
}