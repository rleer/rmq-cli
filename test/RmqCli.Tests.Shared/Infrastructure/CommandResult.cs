using System.Text;

namespace RmqCli.Tests.Shared.Infrastructure;

/// <summary>
/// Wrapper for command execution result
/// </summary>
public record CommandResult
{
    public IEnumerable<string> CliArguments { get; init; } = [];
    public int ExitCode { get; init; }

    public string StdoutOutput { get; init; } = string.Empty;

    public string StderrOutput { get; init; } = string.Empty;

    public string StdinInput { get; init; } = string.Empty;

    public string? ExceptionMessage { get; init; }

    public bool IsSuccess => ExitCode == 0;

    public string ToDebugString()
    {
        var builder = new StringBuilder();

        builder.Append($"Command: rmq {string.Join(' ', CliArguments)}{Environment.NewLine}ExitCode: {ExitCode}{Environment.NewLine}" +
                       $"--- STDIN ---{Environment.NewLine}{StdinInput}{Environment.NewLine}" +
                       $"--- STDOUT ---{Environment.NewLine}{StdoutOutput}{Environment.NewLine}" +
                       $"--- STDERR ---{Environment.NewLine}{StderrOutput}");
        if (!string.IsNullOrEmpty(ExceptionMessage))
        {
            builder.AppendLine().Append($"--- EXCEPTION ---{Environment.NewLine}{ExceptionMessage}");
        }
        
        return builder.ToString();
    }
}