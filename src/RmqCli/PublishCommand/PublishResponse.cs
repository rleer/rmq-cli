using System.Text.Json.Serialization;
using RmqCli.Common;

namespace RmqCli.PublishCommand;

public class PublishResponse : Response
{
    [JsonPropertyName("result")]
    public PublishResult? Result { get; set; }
    
    [JsonPropertyName("destination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DestinationInfo? Destination { get; set; }
}