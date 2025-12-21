namespace RmqCli.Tests.Shared.Infrastructure;

/// <summary>
/// Wrapper for command execution result
/// </summary>
public record CommandResult
{
    public int ExitCode { get; init; }
    // STDOUT output
    public string StdoutOutput { get; init; } = string.Empty;
    // STDERR output
    public string StderrOutput { get; init; } = string.Empty;

    public bool IsSuccess => ExitCode == 0;
    
    public string ToDebugString()
    {
        return
            $"ExitCode: {ExitCode}{Environment.NewLine}--- STDOUT ---{Environment.NewLine}{StdoutOutput}{Environment.NewLine}--- STDERR ---{Environment.NewLine}{StderrOutput}";
    }
}
