using System.Text.Json.Serialization;

namespace RmqCli.Commands.Peek;

public class PeekResult
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

    [JsonPropertyName("output_destination")]
    public string OutputDestination { get; set; } = string.Empty;

    [JsonPropertyName("output_format")]
    public string OutputFormat { get; set; } = string.Empty;

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
