using RabbitMQ.Client;
using System.Text;

namespace Rmq.E2E.Tests;

/// <summary>
/// Arrange-and-assert against the broker directly, so tests never depend on the CLI
/// to verify the CLI. Deliberately not a wrapper over the fixture — it opens its own
/// short-lived connection per call, which is fast enough and keeps every test independent.
/// </summary>
public sealed class Broker(RabbitMqFixture fixture)
{
    private ConnectionFactory Factory => new()
    {
        HostName = fixture.Host,
        Port = fixture.AmqpPort,
        UserName = "guest",
        Password = "guest"
    };

    /// <summary>Declares a fresh, empty queue. Deletes any leftover of the same name first.</summary>
    public async Task<string> DeclareQueue(string name)
    {
        await using var connection = await Factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeleteAsync(name, ifUnused: false, ifEmpty: false);

        // Durable on purpose: RabbitMQ 4 deprecated transient non-exclusive queues and
        // rejects the declare outright.
        await channel.QueueDeclareAsync(name, durable: true, exclusive: false, autoDelete: false);
        return name;
    }

    public async Task DeleteQueue(string name)
    {
        await using var connection = await Factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeleteAsync(name, ifUnused: false, ifEmpty: false);
    }

    public async Task Publish(string queue, params string[] bodies)
    {
        await using var connection = await Factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        foreach (var body in bodies)
        {
            await channel.BasicPublishAsync(exchange: "", routingKey: queue, body: Encoding.UTF8.GetBytes(body));
        }
    }

    public async Task PublishBytes(string queue, byte[] body, BasicProperties? properties = null)
    {
        await using var connection = await Factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        if (properties == null)
        {
            await channel.BasicPublishAsync(exchange: "", routingKey: queue, body: body);
        }
        else
        {
            await channel.BasicPublishAsync(exchange: "", routingKey: queue, mandatory: false, basicProperties: properties, body: body);
        }
    }

    /// <summary>Messages ready in the queue. The assertion `--requeue` lives or dies on.</summary>
    public async Task<uint> Depth(string queue)
    {
        await using var connection = await Factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        return await channel.MessageCountAsync(queue);
    }

    public async Task<List<string>> DrainToList(string queue)
    {
        await using var connection = await Factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var bodies = new List<string>();
        while (await channel.BasicGetAsync(queue, autoAck: true) is { } result)
        {
            bodies.Add(Encoding.UTF8.GetString(result.Body.Span));
        }

        return bodies;
    }
}
