using RmqCli.Core.Models;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.Subcutaneous.Tests.Infrastructure;

/// <summary>
/// Helper methods for subcutaneous tests with RabbitMQ.
/// Unlike E2E tests which run the published executable, subcutaneous tests
/// invoke commands in-process for better code coverage.
/// </summary>
public class RabbitMqTestHelpers
{
    private readonly RabbitMqFixture _fixture;
    private readonly RabbitMqOperations _operations;
    private readonly CliTestHelpers _cliHelpers;

    public RabbitMqTestHelpers(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _operations = new RabbitMqOperations(fixture);
        _cliHelpers = new CliTestHelpers(output);
    }

    /// <summary>
    /// Runs the rmq CLI command in-process with RabbitMQ connection details from the fixture
    /// </summary>
    /// <param name="args">The arguments to execute</param>
    /// <param name="stdinInput">Optional stdin input. When provided, stdin will contain this content.
    /// When null, stdin will be set to TextReader.Null (returns EOF immediately) to prevent hanging.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task<CommandResult> RunRmqCommand(
        IEnumerable<string> args,
        string? stdinInput = null,
        CancellationToken cancellationToken = default)
    {
        // Parse credentials from connection string
        var uri = new Uri(_fixture.ConnectionString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? userInfo[0] : "guest";
        var password = userInfo.Length > 1 ? userInfo[1] : "guest";

        // Create connection details from fixture
        var connectionDetails = new RabbitMqConnectionDetails(
            Host: _fixture.Host,
            AmqpPort: _fixture.AmqpPort,
            ManagementPort: _fixture.ManagementPort,
            Username: username,
            Password: password);

        // Delegate to CliTestHelpers with connection details
        return _cliHelpers.RunRmqCommand(
            args: args,
            connectionDetails: connectionDetails,
            stdinInput: stdinInput,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Publishes messages to a queue using RabbitMQ client directly
    /// </summary>
    public Task PublishMessages(string queueName, params string[] messages) =>
        _operations.PublishMessages(queueName, messages);

    /// <summary>
    /// Gets queue info from RabbitMQ
    /// </summary>
    public Task<QueueInfo> GetQueueInfo(string queueName) => _operations.GetQueueInfo(queueName);

    /// <summary>
    /// Ensures a queue exists by declaring it (idempotent operation)
    /// </summary>
    public Task EnsureQueueExists(string queueName) => _operations.DeclareQueue(queueName);

    /// <summary>
    /// Deletes a queue using RabbitMQ client directly
    /// </summary>
    public Task DeleteQueue(string queueName) => _operations.DeleteQueue(queueName);

    /// <summary>
    /// Purges all messages from a queue
    /// </summary>
    public Task PurgeQueue(string queueName) => _operations.PurgeQueue(queueName);

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
