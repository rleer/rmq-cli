namespace RmqCli.Integration.Tests.Infrastructure.Output;

/// <summary>
/// Collection definition for tests that redirect Console streams (Console.Out, Console.Error).
/// Parallelization is disabled because Console streams are global static resources that cannot
/// be safely shared across parallel tests.
/// </summary>
[CollectionDefinition("ConsoleOutputTests", DisableParallelization = true)]
public class ConsoleOutputCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] with DisableParallelization.
}

