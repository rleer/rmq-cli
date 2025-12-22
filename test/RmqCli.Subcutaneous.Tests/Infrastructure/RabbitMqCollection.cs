using RmqCli.Tests.Shared.Infrastructure;

namespace RmqCli.Subcutaneous.Tests.Infrastructure;

/// <summary>
/// Collection definition for sharing RabbitMQ container across subcutaneous tests.
/// Tests in this collection will share the same RabbitMQ instance for better performance.
/// </summary>
[CollectionDefinition("RabbitMQ", DisableParallelization = true)]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
