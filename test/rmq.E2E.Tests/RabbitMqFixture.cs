using Testcontainers.RabbitMq;

namespace Rmq.E2E.Tests;

/// <summary>
/// One RabbitMQ container shared by the whole suite. Starting a broker per test class
/// would dominate the run time and buys nothing — tests isolate on queue names instead.
/// </summary>
public class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer? _container;

    public string Host { get; private set; } = string.Empty;
    public int AmqpPort { get; private set; }
    public int ManagementPort { get; private set; }

    public string AmqpUrl => $"amqp://guest:guest@{Host}:{AmqpPort}/";
    public string ManagementUrl => $"http://guest:guest@{Host}:{ManagementPort}/";

    public async Task InitializeAsync()
    {
        _container = new RabbitMqBuilder("rabbitmq:4-management")
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            .WithCleanUp(true)
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await _container.StartAsync();

        Host = new Uri(_container.GetConnectionString()).Host;
        AmqpPort = _container.GetMappedPublicPort(5672);
        ManagementPort = _container.GetMappedPublicPort(15672);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "RabbitMQ";
}
