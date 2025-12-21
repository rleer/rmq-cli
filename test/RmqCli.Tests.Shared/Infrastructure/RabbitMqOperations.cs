using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Tests.Shared.Infrastructure;

/// <summary>
/// Helper methods for common RabbitMQ operations in tests.
/// Provides utilities for declaring queues, publishing messages, and querying queue state.
/// </summary>
public class RabbitMqOperations
{
    private readonly RabbitMqFixture _fixture;

    public RabbitMqOperations(RabbitMqFixture fixture)
    {
        _fixture = fixture;
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

    /// <summary>
    /// Declares an exchange in RabbitMQ for testing
    /// </summary>
    public async Task DeclareExchange(string exchangeName, string type)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Ensure exchange exists
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: type,
            durable: false,
            autoDelete: false);
    }

    /// <summary>
    /// Deletes an exchange
    /// </summary>
    public async Task DeleteExchange(string exchangeName)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.ExchangeDeleteAsync(exchangeName);
        }
        catch
        {
            // Exchange might not exist, ignore
        }
    }

    /// <summary>
    /// Declares a binding between an exchange and a queue
    /// </summary>
    public async Task DeclareBinding(string exchangeName, string queueName, string routingKey)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Ensure binding exists
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);
    }

    /// <summary>
    /// Deletes a binding between an exchange and a queue
    /// </summary>
    public async Task DeleteBinding(string exchangeName, string queueName, string routingKey)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_fixture.ConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.QueueUnbindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: routingKey);
        }
        catch
        {
            // Binding might not exist, ignore
        }
    }
}