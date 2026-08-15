using CliWrap;
using System.Text;
using Xunit.Abstractions;

namespace Rmq.E2E.Tests;

public sealed record CliResult(int ExitCode, string Stdout, string Stderr)
{
    /// <summary>NDJSON output, one message per line, with blank lines dropped.</summary>
    public string[] StdoutLines =>
        Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// Runs the *published native binary*, not the assembly — these tests exercise what
/// ships, including AOT behaviour and real process exit codes. `just prepare-e2e-test`
/// publishes it into bin/rmq-published first.
/// </summary>
public sealed class Cli(ITestOutputHelper output)
{
    /// <param name="cancel">
    /// Cancelling this sends the process a real Ctrl-C rather than killing it, which is
    /// the only way to test the exit-130 path and the "ack everything already written"
    /// guarantee that goes with it.
    /// </param>
    public async Task<CliResult> Run(
        IEnumerable<string> args,
        string? stdin = null,
        TimeSpan? timeout = null,
        CancellationToken cancel = default)
    {
        var argv = args.ToList();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var kill = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(1));

        // Fully qualified: this class is also called Cli, and would otherwise win the lookup.
        var command = CliWrap.Cli.Wrap(ExecutablePath)
            .WithArguments(argv)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None);

        if (stdin != null)
        {
            command = command.WithStandardInputPipe(PipeSource.FromString(stdin));
        }

        int exitCode;
        try
        {
            exitCode = (await command.ExecuteAsync(kill.Token, cancel)).ExitCode;
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // CliWrap surfaces graceful termination as a cancellation and gives us no
            // exit code, so record the one the contract promises for Ctrl-C.
            exitCode = 130;
        }

        var result = new CliResult(exitCode, stdout.ToString(), stderr.ToString());
        output.WriteLine($"$ rmq {string.Join(' ', argv)}\n-> exit {result.ExitCode}\n[stdout]\n{result.Stdout}\n[stderr]\n{result.Stderr}");
        return result;
    }

    private static string ExecutablePath
    {
        get
        {
            var path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "rmq-published", "rmq"));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"rmq binary not found at {path}. Run `just prepare-e2e-test` first.", path);
            }

            return path;
        }
    }
}
