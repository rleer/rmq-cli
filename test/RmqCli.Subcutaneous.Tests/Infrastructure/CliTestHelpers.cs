using System.Text;
using RmqCli.Commands;
using RmqCli.Shared.Factories;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;
using CommandResult = RmqCli.Tests.Shared.Infrastructure.CommandResult;

namespace RmqCli.Subcutaneous.Tests.Infrastructure;

/// <summary>
/// Helper methods for running the rmq CLI in subcutaneous tests without requiring a RabbitMQ instance.
/// Unlike E2E tests which run the published executable, this runs commands in-process for better code coverage.
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
    /// Runs the rmq CLI command in-process without connection parameters (for help, config, etc.)
    /// This method captures stdout and stderr to simulate the CLI experience.
    ///
    /// IMPORTANT: Console.IsInputRedirected will always be true in xUnit tests due to OS-level redirection.
    /// </summary>
    /// <param name="args">The arguments to pass to the command</param>
    /// <param name="stdinInput">Optional stdin input. When provided, stdin will contain this content.
    /// When null, stdin will be set to TextReader.Null (returns EOF immediately) to prevent hanging.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task<CommandResult> RunRmqCommand(
        IEnumerable<string> args,
        string? stdinInput = null,
        CancellationToken cancellationToken = default)
    {
        return RunRmqCommand(
            args: args,
            connectionDetails: null,
            stdinInput: stdinInput,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Runs the rmq CLI command in-process with explicit connection details.
    /// This method captures stdout and stderr to simulate the CLI experience.
    ///
    /// IMPORTANT: Console.IsInputRedirected will always be true in xUnit tests due to OS-level redirection.
    /// </summary>
    /// <param name="args">The arguments to execute</param>
    /// <param name="connectionDetails">RabbitMQ connection details (host, ports, credentials)</param>
    /// <param name="stdinInput">Optional stdin input. When provided, stdin will contain this content.
    /// When null, stdin will be set to TextReader.Null (returns EOF immediately) to prevent hanging.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CommandResult> RunRmqCommand(
        IEnumerable<string> args,
        RabbitMqConnectionDetails? connectionDetails,
        string? stdinInput = null,
        CancellationToken cancellationToken = default)
    {
        // Build final args list, optionally including connection args
        var finalArgs = args.ToList();

        if (connectionDetails is not null)
        {
            finalArgs.AddRange(new[]
            {
                "--host", connectionDetails.Host,
                "--port", connectionDetails.AmqpPort.ToString(),
                "--management-port", connectionDetails.ManagementPort.ToString(),
                "--user", connectionDetails.Username,
                "--password", connectionDetails.Password
            });
        }

        // Capture stdout, stderr and stdin
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalIn = Console.In;
        var stdOutBuffer = new StringBuilder();
        var stdErrBuffer = new StringBuilder();

        // Setup stdin based on whether input is provided
        MemoryStream? stdinStream = null;
        StreamReader? stdinReader = null;

        try
        {
            if (stdinInput is not null)
            {
                // Stdin with actual content (simulates piped input)
                stdinStream = new MemoryStream(Encoding.UTF8.GetBytes(stdinInput));
                stdinReader = new StreamReader(stdinStream);
            }

            // Redirect console output
            await using var stdOutWriter = new StringWriter(stdOutBuffer);
            await using var stdErrWriter = new StringWriter(stdErrBuffer);
            Console.SetOut(stdOutWriter);
            Console.SetError(stdErrWriter);

            Console.SetIn(stdinReader ?? TextReader.Null); // If no input, set to Null to prevent hanging

            // Create service factory and root command handler
            var serviceFactory = new ServiceFactory();
            var rootCommandHandler = new RootCommandHandler(serviceFactory);

            _output.WriteLine("Command: rmq " + string.Join(' ', finalArgs));
            
            // Run command in-process
            var exitCode = await rootCommandHandler.RunAsync(finalArgs.ToArray(), cancellationToken);

            var result = new CommandResult
            {
                CliArguments = finalArgs,
                ExitCode = exitCode,
                StdoutOutput = stdOutBuffer.ToString().Trim(),
                StderrOutput = stdErrBuffer.ToString().Trim(),
                StdinInput = stdinInput ?? string.Empty
            };

            _output.WriteLine(result.ToDebugString());
            return result;
        }
        catch (Exception ex)
        {
            // If an exception occurs, return error result
            var result = new CommandResult
            {
                CliArguments = finalArgs,
                ExitCode = 42,
                StdoutOutput = stdOutBuffer.ToString().Trim(),
                StderrOutput = $"{stdErrBuffer}{Environment.NewLine}{ex.Message}".Trim(),
                StdinInput = stdinInput ?? string.Empty,
                ExceptionMessage = ex.Message
            };

            _output.WriteLine(result.ToDebugString());
            return result;
        }
        finally
        {
            // Restore console output
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Console.SetIn(originalIn);

            // Dispose stdin resources if they were created
            stdinReader?.Dispose();
            stdinStream?.Dispose();
        }
    }
}
