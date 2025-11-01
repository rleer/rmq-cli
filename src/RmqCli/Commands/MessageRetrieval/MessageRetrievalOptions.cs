using RmqCli.Core.Models;

namespace RmqCli.Commands.MessageRetrieval;

public class MessageRetrievalOptions
{
    public required string Queue { get; init; }
    public AckModes AckMode { get; init; }
    public int MessageCount { get; init; }
    public ushort PrefetchCount { get; init; }
}