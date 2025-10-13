using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using RmqCli.Commands.Consume;
using RmqCli.Commands.Publish;
using RmqCli.DependencyInjection;

namespace RmqCli;

/// <summary>
/// Factory for creating command-specific services with dependency injection.
/// </summary>
public class ServiceFactory
{
    /// <summary>
    /// Creates a configured consume service with all required dependencies.
    /// </summary>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <returns>A configured consume service instance.</returns>
    public IConsumeService CreateConsumeService(ParseResult parseResult)
    {
        var services = new ServiceCollection();
        services.AddRmqConsume(parseResult);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IConsumeService>();
    }

    /// <summary>
    /// Creates a configured publish service with all required dependencies.
    /// </summary>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <returns>A configured publish service instance.</returns>
    public IPublishService CreatePublishService(ParseResult parseResult)
    {
        var services = new ServiceCollection();
        services.AddRmqPublish(parseResult);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IPublishService>();
    }
}