using System.Text.Json.Serialization;

namespace RmqCli.Core.Models.Management;

/// <summary>
/// Queue details model for RabbitMQ management API responses.
/// </summary>
public class QueueDetails
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("vhost")]
    public string Vhost { get; set; } = string.Empty;
}