namespace RmqCli.Commands.Peek;

/// <summary>
/// Options specific to the peek command.
/// </summary>
public class PeekOptions
{
    public required string Queue { get; init; }
    public int MessageCount { get; init; }
}
