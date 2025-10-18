namespace RmqCli.Commands.Consume;

/// <summary>
/// Options specific to the consume command.
/// </summary>
public class ConsumeOptions
{
    public required string Queue { get; init; }
    public AckModes AckMode { get; init; }
    public int MessageCount { get; init; }
}
