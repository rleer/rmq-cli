using System.Text;
using CliWrap;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;
using CommandResult = RmqCli.Tests.Shared.Infrastructure.CommandResult;

namespace RmqCli.E2E.Tests.Infrastructure;

/// <summary>
/// Helper methods for running the rmq CLI in E2E tests without requiring a RabbitMQ instance.
/// Use this for testing commands that don't need RabbitMQ (e.g., --help, config commands).
/// </summary>
public class CliTestHelpers
{
    private readonly ITestOutputHelper _output;

    public CliTestHelpers(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Runs the rmq CLI command without connection parameters (for help, config, etc.)
    /// </summary>
    /// <param name="args">The arguments to pass to the command</param>
    /// <param name="stdinInput">Optional stdin input to pipe to the command</param>
    /// <param name="timeout">Optional timeout (default: 1 minute)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task<CommandResult> RunRmqCommand(
        IEnumerable<string> args,
        string? stdinInput = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return RunRmqCommand(
            args: args,
            connectionDetails: null,
            stdinInput: stdinInput,
            timeout: timeout,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Runs the rmq CLI command with explicit connection details
    /// </summary>
    /// <param name="args">The arguments to pass to the command</param>
    /// <param name="connectionDetails">RabbitMQ connection details (host, ports, credentials)</param>
    /// <param name="stdinInput">Optional stdin input to pipe to the command</param>
    /// <param name="timeout">Optional timeout (default: 1 minute)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CommandResult> RunRmqCommand(
        IEnumerable<string> args,
        RabbitMqConnectionDetails? connectionDetails,
        string? stdinInput = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromMinutes(1);

        // Build full arguments list, optionally including connection details
        var fullArgs = new List<string>(args);

        if (connectionDetails is not null)
        {
            fullArgs.AddRange(new[]
            {
                "--host", connectionDetails.Host,
                "--port", connectionDetails.AmqpPort.ToString(),
                "--management-port", connectionDetails.ManagementPort.ToString(),
                "--user", connectionDetails.Username,
                "--password", connectionDetails.Password
            });
        }

        // Get path to published rmq executable
        var rmqPath = GetRmqExecutablePath();

        // Create linked cancellation token source for graceful cancellation that can be triggered externally
        using var gracefulCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Fallback if graceful cancellation doesn't complete in time
        using var timeoutCts = new CancellationTokenSource(timeout.Value);

        var stdOutBuffer = new StringBuilder();
        var stdErrBuffer = new StringBuilder();

        try
        {
            var cliCommand = Cli.Wrap(rmqPath)
                .WithArguments(fullArgs)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                .WithValidation(CommandResultValidation.None); // Don't throw on non-zero exit codes

            // Configure stdin based on whether input is provided
            if (stdinInput is not null)
            {
                // Pipe stdin content
                cliCommand = cliCommand.WithStandardInputPipe(PipeSource.FromString(stdinInput));
            }

            var result = await cliCommand.ExecuteAsync(timeoutCts.Token, gracefulCts.Token); // Graceful cancellation token will send Ctrl-C to rmq process

            var commandResult = new CommandResult
            {
                ExitCode = result.ExitCode,
                StdoutOutput = stdOutBuffer.ToString().Trim(),
                StderrOutput = stdErrBuffer.ToString().Trim()
            };

            _output.WriteLine(commandResult.ToDebugString());
            return commandResult;
        }
        catch (OperationCanceledException ex)
        {
            // CliWrap throws OperationCanceledException when the command is canceled so result is not available here
            var exitCode = 2;
            if (gracefulCts.IsCancellationRequested && ex.Message.Contains("was gracefully terminated"))
            {
                // rmq returns 0 on graceful termination
                exitCode = 0;
            }

            var commandResult = new CommandResult
            {
                ExitCode = exitCode,
                StdoutOutput = stdOutBuffer.ToString().Trim(),
                StderrOutput = stdErrBuffer.ToString().Trim()
            };

            _output.WriteLine(commandResult.ToDebugString());
            return commandResult;
        }
    }

    /// <summary>
    /// Gets the path to the published rmq executable
    /// </summary>
    private static string GetRmqExecutablePath()
    {
        // Start from the test assembly location
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;

        // Navigate to the published executable at test/RmqCli.E2E.Tests/bin/rmq-published/rmq
        var testBinDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "rmq-published"));
        var rmqPath = Path.Combine(testBinDir, "rmq");

        if (!File.Exists(rmqPath))
        {
            throw new FileNotFoundException(
                $"RMQ executable not found at: {rmqPath}. " +
                "Please run 'dotnet publish src/RmqCli/RmqCli.csproj -c Release -r <RUNTIME_IDENTIFIER> -o test/RmqCli.E2E.Tests/bin/rmq-published' first.");
        }

        return rmqPath;
    }
}