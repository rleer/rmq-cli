using System.Text;
using CliWrap;
using RmqCli.Core.Models;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;
using CommandResult = RmqCli.Tests.Shared.Infrastructure.CommandResult;

namespace RmqCli.E2E.Tests.Infrastructure;

/// <summary>
/// Helper methods for E2E tests with RabbitMQ
/// </summary>
public class RabbitMqTestHelpers
{
    private readonly RabbitMqFixture _fixture;
    private readonly RabbitMqOperations _operations;
    private readonly ITestOutputHelper _output;

    public RabbitMqTestHelpers(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _operations = new RabbitMqOperations(fixture);
    }

    /// <summary>
    /// Runs the rmq CLI command and returns the result
    /// </summary>
    /// <param name="args">The arguments to pass to the command</param>
    /// <param name="stdinInput">Optional stdin input to pipe to the command</param>
    /// <param name="timeout">Optional timeout (default: 1 minute)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CommandResult> RunRmqCommand(
        IEnumerable<string> args,
        string? stdinInput = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromMinutes(1);

        // Parse credentials from connection string
        var uri = new Uri(_fixture.ConnectionString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? userInfo[0] : "guest";
        var password = userInfo.Length > 1 ? userInfo[1] : "guest";

        // Build full arguments list with connection details
        var fullArgs = new List<string>(args)
        {
            "--host", _fixture.Host,
            "--port", _fixture.AmqpPort.ToString(),
            "--user", username,
            "--password", password
        };

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

    /// <summary>
    /// Gets queue information directly from RabbitMQ
    /// </summary>
    public Task<QueueInfo> GetQueueInfo(string queueName) => _operations.GetQueueInfo(queueName);

    /// <summary>
    /// Declares a queue in RabbitMQ for testing
    /// </summary>
    public Task DeclareQueue(string queueName) => _operations.DeclareQueue(queueName);

    /// <summary>
    /// Publishes messages directly to RabbitMQ for testing
    /// </summary>
    public Task PublishMessages(string queueName, IEnumerable<string> messages, bool declareQueue = true) =>
        _operations.PublishMessages(queueName, messages, declareQueue);

    /// <summary>
    /// Purges all messages from a queue
    /// </summary>
    public Task PurgeQueue(string queueName) => _operations.PurgeQueue(queueName);

    /// <summary>
    /// Deletes a queue
    /// </summary>
    public Task DeleteQueue(string queueName) => _operations.DeleteQueue(queueName);

    /// <summary>
    /// Declares an exchange in RabbitMQ for testing
    /// </summary>
    public Task DeclareExchange(string exchangeName, string type) =>
        _operations.DeclareExchange(exchangeName, type);

    /// <summary>
    /// Deletes an exchange
    /// </summary>
    public Task DeleteExchange(string exchangeName) => _operations.DeleteExchange(exchangeName);

    /// <summary>
    /// Declares a binding between an exchange and a queue
    /// </summary>
    public Task DeclareBinding(string exchangeName, string queueName, string routingKey) =>
        _operations.DeclareBinding(exchangeName, queueName, routingKey);

    /// <summary>
    /// Deletes a binding between an exchange and a queue
    /// </summary>
    public Task DeleteBinding(string exchangeName, string queueName, string routingKey) =>
        _operations.DeleteBinding(exchangeName, queueName, routingKey);
}
