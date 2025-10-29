using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using RmqCli.Commands.Consume;
using RmqCli.Commands.Peek;
using RmqCli.Commands.Publish;
using RmqCli.DependencyInjection;
using RmqCli.Infrastructure.Output;
using RmqCli.Shared;

namespace RmqCli;

/// <summary>
/// Factory for creating command-specific services with dependency injection.
/// Handles extraction of options from ParseResult and service configuration.
/// </summary>
public class ServiceFactory
{
    /// <summary>
    /// Creates a configured consume service with all required dependencies.
    /// Extracts options from ParseResult and registers them in the DI container.
    /// </summary>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <returns>A configured consume service instance.</returns>
    public IConsumeService CreateConsumeService(ParseResult parseResult)
    {
        // Extract command-specific options from ParseResult
        var consumeOptions = CreateConsumeOptions(parseResult);
        var outputOptions = CreateOutputOptions(parseResult);

        var services = new ServiceCollection();
        services.AddRmqConsume(parseResult, consumeOptions, outputOptions);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IConsumeService>();
    }

    /// <summary>
    /// Creates ConsumeOptions from ParseResult.
    /// </summary>
    private static ConsumeOptions CreateConsumeOptions(ParseResult parseResult)
    {
        var ackMode = parseResult.GetValue<AckModes>("--ack-mode");
        var prefetchCountFromUser = parseResult.GetValue<ushort?>("--prefetch-count");

        // Resolve prefetch count:
        // - If requeue mode and user didn't specify: set to 0 (unlimited) to avoid infinite loop
        // - If user didn't specify: use default of 100
        // - Otherwise: use user-provided value
        ushort prefetchCount;
        if (ackMode == AckModes.Requeue && !prefetchCountFromUser.HasValue)
        {
            prefetchCount = 0; // Unlimited - avoid prefetch with requeue
        }
        else if (!prefetchCountFromUser.HasValue)
        {
            prefetchCount = 100; // Default for normal consumption
        }
        else
        {
            prefetchCount = prefetchCountFromUser.Value;
        }

        return new ConsumeOptions
        {
            Queue = parseResult.GetRequiredValue<string>("--queue"),
            AckMode = ackMode,
            MessageCount = parseResult.GetValue<int>("--count"),
            PrefetchCount = prefetchCount
        };
    }

    /// <summary>
    /// Creates a configured peek service with all required dependencies.
    /// Extracts options from ParseResult and registers them in the DI container.
    /// </summary>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <returns>A configured peek service instance.</returns>
    public IPeekService CreatePeekService(ParseResult parseResult)
    {
        // Extract command-specific options from ParseResult
        var peekOptions = CreatePeekOptions(parseResult);
        var outputOptions = CreateOutputOptions(parseResult);

        var services = new ServiceCollection();
        services.AddRmqPeek(parseResult, peekOptions, outputOptions);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IPeekService>();
    }

    /// <summary>
    /// Creates PeekOptions from ParseResult.
    /// </summary>
    private static PeekOptions CreatePeekOptions(ParseResult parseResult)
    {
        return new PeekOptions
        {
            Queue = parseResult.GetRequiredValue<string>("--queue"),
            MessageCount = parseResult.GetValue<int>("--count")
        };
    }

    /// <summary>
    /// Creates OutputOptions from ParseResult.
    /// This method can be reused across all commands (consume, publish, etc.).
    /// </summary>
    private static OutputOptions CreateOutputOptions(ParseResult parseResult)
    {
        // Extract output file path and convert to FileInfo if provided
        var outputFilePath = parseResult.GetValue<string>("--to-file");
        FileInfo? outputFileInfo = null;
        if (!string.IsNullOrWhiteSpace(outputFilePath))
        {
            outputFileInfo = new FileInfo(Path.GetFullPath(outputFilePath, Environment.CurrentDirectory));
        }

        return new OutputOptions
        {
            Format = parseResult.GetValue<OutputFormat>("--output"),
            Quiet = parseResult.GetValue<bool>("--quiet"),
            Verbose = parseResult.GetValue<bool>("--verbose"),
            NoColor = parseResult.GetValue<bool>("--no-color"),
            OutputFile = outputFileInfo,
            Compact = parseResult.GetValue<bool>("--compact")
        };
    }

    /// <summary>
    /// Creates a configured publish service with all required dependencies.
    /// Extracts options from ParseResult and registers them in the DI container.
    /// </summary>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <returns>A configured publish service instance.</returns>
    public IPublishService CreatePublishService(ParseResult parseResult)
    {
        // Extract output options (reused across commands)
        var outputOptions = CreateOutputOptions(parseResult);

        var services = new ServiceCollection();
        services.AddRmqPublish(parseResult, outputOptions);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IPublishService>();
    }

    /// <summary>
    /// Creates PublishOptions from ParseResult.
    /// </summary>
    public static PublishOptions CreatePublishOptions(ParseResult parseResult)
    {
        var filePath = parseResult.GetValue<string>("--from-file");
        var exchangeName = parseResult.GetValue<string>("--exchange");
        var queueName = parseResult.GetValue<string>("--queue");
        var routingKey = parseResult.GetValue<string>("--routing-key");
        var messageBody = parseResult.GetValue<string>("--body");
        var burstCount = parseResult.GetValue<int>("--burst");

        FileInfo? inputFile = null;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            inputFile = new FileInfo(Path.GetFullPath(filePath, Environment.CurrentDirectory));
        }

        var destination = new Core.Models.DestinationInfo
        {
            Exchange = exchangeName,
            Queue = queueName,
            RoutingKey = routingKey,
            Type = string.IsNullOrWhiteSpace(queueName) ? "exchange" : "queue"
        };

        return new PublishOptions
        {
            Destination = destination,
            MessageBody = messageBody,
            InputFile = inputFile,
            BurstCount = burstCount,
            IsStdinRedirected = Console.IsInputRedirected
        };
    }
}