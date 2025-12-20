using System.Net;
using DotNet.Testcontainers.Builders;
using Testcontainers.RabbitMq;

namespace RmqCli.Tests.Shared.Infrastructure;

/// <summary>
/// Provides a RabbitMQ container for tests.
/// Container is shared across all tests in a collection to improve performance.
/// </summary>
public class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer? _container;

    public string Host { get; private set; } = string.Empty;
    public int AmqpPort { get; private set; }
    public int ManagementPort { get; private set; }
    public string ConnectionString { get; private set; } = string.Empty;
    public string ManagementUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new RabbitMqBuilder()
            .WithImage("rabbitmq:4-management")
            .WithPortBinding(5672, true) // Random host port for AMQP
            .WithPortBinding(15672, true) // Random host port for Management UI
            .WithCleanUp(true)
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        // Parse connection details
        var uri = new Uri(ConnectionString);
        Host = uri.Host;
        AmqpPort = _container.GetMappedPublicPort(5672);
        ManagementPort = _container.GetMappedPublicPort(15672);
        ManagementUrl = $"http://{Host}:{ManagementPort}";

        // Wait for RabbitMQ to be fully ready by testing the connection
        _ = Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
            request => request
                .ForPath("/")
                .ForPort(15672)
                .ForStatusCode(HttpStatusCode.OK));
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
