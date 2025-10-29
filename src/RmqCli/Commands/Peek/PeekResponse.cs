using System.Text.Json.Serialization;
using RmqCli.Core.Models;

namespace RmqCli.Commands.Peek;

public class PeekResponse : Response
{
    [JsonPropertyName("result")]
    public PeekResult? Result { get; set; }

    [JsonPropertyName("queue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Queue { get; set; }
}
