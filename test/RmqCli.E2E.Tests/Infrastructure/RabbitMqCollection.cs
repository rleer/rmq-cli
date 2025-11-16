namespace RmqCli.E2E.Tests.Infrastructure;

/// <summary>
/// Collection definition for sharing RabbitMQ container across tests.
/// Tests in this collection will share the same RabbitMQ instance for better performance.
/// </summary>
[CollectionDefinition("RabbitMQ")]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
