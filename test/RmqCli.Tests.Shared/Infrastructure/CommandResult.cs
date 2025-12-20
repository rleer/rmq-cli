namespace RmqCli.Tests.Shared.Infrastructure;

/// <summary>
/// Wrapper for command execution result
/// </summary>
public record CommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string ErrorOutput { get; init; } = string.Empty;

    public bool IsSuccess => ExitCode == 0;
}
