using System.Text.Json.Serialization;

namespace RmqCli.Common;

public class Response
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // "success", "partial", "error" 

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonPropertyName("error")]
    public ErrorInfo? Error { get; set; }
}