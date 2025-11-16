using CliWrap;
using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.E2E.Tests.Infrastructure;

/// <summary>
/// Helper methods for E2E tests with RabbitMQ
/// </summary>
public class RabbitMqTestHelpers
{
    private readonly RabbitMqFixture _fixture;

    public RabbitMqTestHelpers(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Runs the rmq CLI command and returns the result
    /// </summary>
    public async Task<CommandResult> RunRmqCommand(
        string command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromMinutes(1);

        // Parse credentials from connection string
        var uri = new Uri(_fixture.ConnectionString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? userInfo[0] : "guest";
        var password = userInfo.Length > 1 ? userInfo[1] : "guest";

        // Build full arguments with connection details
        var fullArgs = $"{command} --host {_fixture.Host} --port {_fixture.AmqpPort} --user {username} --password {password}";

        // Get path to published rmq executable
        var rmqPath = GetRmqExecutablePath();

        // Create linked cancellation token source for graceful cancellation that can be triggered externally
        using var gracefulCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Fallback if graceful cancellation doesn't complete in time
        using var timeoutCts = new CancellationTokenSource(timeout.Value);

        var stdOutBuffer = new System.Text.StringBuilder();
        var stdErrBuffer = new System.Text.StringBuilder();

        try
        {
            var result = await Cli.Wrap(rmqPath)
                .WithArguments(fullArgs)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                .WithValidation(CommandResultValidation.None) // Don't throw on non-zero exit codes
                .ExecuteAsync(timeoutCts.Token, gracefulCts.Token); // Graceful cancellation token will send Ctrl-C to rmq process

            return new CommandResult
            {
                ExitCode = result.ExitCode,
                Output = stdOutBuffer.ToString().Trim(),
                ErrorOutput = stdErrBuffer.ToString().Trim()
            };
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

            return new CommandResult
            {
                ExitCode = exitCode,
                Output = stdOutBuffer.ToString().Trim(),
                ErrorOutput = stdErrBuffer.ToString().Trim()
            };
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
                "Please run 'dotnet publish src/RmqCli/RmqCli.csproj -c Release -r osx-arm64 -o test/RmqCli.E2E.Tests/bin/rmq-published' first.");
        }

        return rmqPath;
    }

    /// <summary>
    /// Gets queue information directly from RabbitMQ
    /// </summary>
    public async Task<QueueInfo> GetQueueInfo(string queueName)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            var result = await channel.QueueDeclarePassiveAsync(queueName);

            return new QueueInfo
            {
                Queue = queueName,
                Exists = true,
                MessageCount = (int)result.MessageCount,
                ConsumerCount = (int)result.ConsumerCount
            };
        }
        catch (Exception)
        {
            return new QueueInfo
            {
                Queue = queueName,
                Exists = false,
                MessageCount = 0,
                ConsumerCount = 0
            };
        }
    }
    
    /// <summary>
    /// Declares a queue in RabbitMQ for testing
    /// </summary>
    public async Task DeclareQueue(string queueName)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Ensure queue exists
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false);
    }

    /// <summary>
    /// Publishes messages directly to RabbitMQ for testing
    /// </summary>
    public async Task PublishMessages(string queueName, IEnumerable<string> messages, bool declareQueue = true)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        if (declareQueue)
        {
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false);
        }

        foreach (var message in messages)
        {
            var body = System.Text.Encoding.UTF8.GetBytes(message);
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                body: body);
        }
    }

    /// <summary>
    /// Purges all messages from a queue
    /// </summary>
    public async Task PurgeQueue(string queueName)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.QueuePurgeAsync(queueName);
        }
        catch
        {
            // Queue might not exist, ignore
        }
    }

    /// <summary>
    /// Deletes a queue
    /// </summary>
    public async Task DeleteQueue(string queueName)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.QueueDeleteAsync(queueName);
        }
        catch
        {
            // Queue might not exist, ignore
        }
    }
}

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