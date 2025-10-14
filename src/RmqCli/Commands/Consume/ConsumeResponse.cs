using System.Text.Json.Serialization;
using RmqCli.Core.Models;

namespace RmqCli.Commands.Consume;

public class ConsumeResponse : Response
{
    [JsonPropertyName("result")]
    public ConsumeResult? Result { get; set; }

    [JsonPropertyName("queue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Queue { get; set; }
}