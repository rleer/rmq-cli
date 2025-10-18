using RmqCli.Core.Models;

namespace RmqCli.Commands.Publish;

/// <summary>
/// Options specific to the publish command.
/// </summary>
public class PublishOptions
{
    public DestinationInfo Destination { get; init; } = null!;
    public string? MessageBody { get; init; }
    public FileInfo? InputFile { get; init; }
    public int BurstCount { get; init; }
    public bool IsStdinRedirected { get; init; }
}
