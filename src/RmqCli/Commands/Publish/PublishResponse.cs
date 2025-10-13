using System.Text.Json.Serialization;
using RmqCli.Core.Models;

namespace RmqCli.Commands.Publish;

public class PublishResponse : Response
{
    [JsonPropertyName("result")]
    public PublishResult? Result { get; set; }
    
    [JsonPropertyName("destination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DestinationInfo? Destination { get; set; }
}