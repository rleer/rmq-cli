using System.Text.Json.Serialization;

namespace RmqCli.Core.Models;

public class Response
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // "success", "partial", "failure" 

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonPropertyName("error")]
    public ErrorInfo? Error { get; set; }
}