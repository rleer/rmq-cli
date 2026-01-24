using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RmqCli.Commands.Consume;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Commands.MessageRetrieval.Strategies;
using RmqCli.Commands.Peek;
using RmqCli.Commands.Publish;
using RmqCli.Commands.Purge;
using RmqCli.Infrastructure.Configuration;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared.Output;

namespace RmqCli.Shared.Extensions;

/// <summary>
/// Extension methods for configuring RmqCli services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RmqCli configuration services to the service collection.
    /// Registers configuration from TOML files and binds to configuration POCOs.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="parseResult">The parse result containing CLI options including custom config path.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddRmqConfiguration(this IServiceCollection services, ParseResult parseResult)
    {
        var customConfigPath = parseResult.GetValue<string>("--config");
        var userConfigPath = parseResult.GetValue<string>("--user-config-path");

        // Build configuration from TOML files
        var configuration = new ConfigurationBuilder()
            .AddRmqConfig(customConfigPath, userConfigPath)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Bind and register RabbitMQ configuration (uses source generation - no reflection needed at runtime)
        var rabbitMqConfig = new RabbitMqConfig();
        configuration.GetSection(RabbitMqConfig.RabbitMqConfigName).Bind(rabbitMqConfig);

        // Override with CLI options if provided
        ApplyRabbitMqConfigOverrides(rabbitMqConfig, parseResult);

        services.AddSingleton(rabbitMqConfig);

        // Bind and register file configuration (uses source generation - no reflection needed at runtime)
        var fileConfig = new FileConfig();
        configuration.GetSection(nameof(FileConfig)).Bind(fileConfig);
        services.AddSingleton(fileConfig);

        return services;
    }

    private static void ApplyRabbitMqConfigOverrides(RabbitMqConfig config, ParseResult parseResult)
    {
        if (parseResult.GetValue<string>("--vhost") is { } vhost)
        {
            config.VirtualHost = vhost;
        }

        if (parseResult.GetValue<string>("--host") is { } host)
        {
            config.Host = host;
        }

        if (parseResult.GetValue<int>("--port") is var port && port != 0)
        {
            config.Port = port;
        }

        if (parseResult.GetValue<int>("--management-port") is var managementPort && managementPort != 0)
        {
            config.ManagementPort = managementPort;
        }

        if (parseResult.GetValue<string>("--user") is { } user)
        {
            config.User = user;
        }

        if (parseResult.GetValue<string>("--password") is { } password)
        {
            config.User = password;
        }
    }

    /// <summary>
    /// Adds RmqCli logging configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="verbose">Whether to enable verbose (Debug level) logging.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddRmqLogging(this IServiceCollection services, bool verbose)
    {
        var logLevel = verbose ? LogLevel.Trace : LogLevel.None;

        services.AddLogging(builder =>
        {
            builder.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; })
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .SetMinimumLevel(logLevel);

            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
                options.IncludeScopes = false;
            });
        });

        return services;
    }

    /// <summary>
    /// Adds core RmqCli services to the service collection.
    /// Includes RabbitMQ channel factory and status output service.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="useAmqpClient">Add AMQP client factory</param>
    /// <param name="useManagementClient">Add management API client</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddRmqCoreServices(this IServiceCollection services, bool useAmqpClient = true, bool useManagementClient = false)
    {
        if (useManagementClient)
        {
            services.AddSingleton<IRabbitManagementClient, RabbitManagementClient>();
        }

        if (useAmqpClient)
        {
            services.AddSingleton<IRabbitChannelFactory, RabbitChannelFactory>();
        }

        services.AddSingleton<IStatusOutputService, StatusOutputService>();

        return services;
    }

    /// <summary>
    /// Adds publish command services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddPublishServices(this IServiceCollection services)
    {
        services.AddSingleton<IPublishOutputService, PublishOutputService>();
        services.AddSingleton<IPublishService, PublishService>();

        return services;
    }

    /// <summary>
    /// Adds consume command services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddConsumeServices(this IServiceCollection services)
    {
        services.AddSingleton<IConsumeService, ConsumeService>();
        services.AddSingleton<IMessageRetrievalStrategy, SubscriberStrategy>();

        return services;
    }

    /// <summary>
    /// Adds message retrieval related services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddMessageRetrievalServices(this IServiceCollection services)
    {
        services.AddSingleton<MessageRetrievalResultOutputService>();
        services.AddSingleton<QueueValidator>();
        services.AddSingleton<AckHandler>();
        services.AddSingleton<MessagePipeline>();

        return services;
    }

    /// <summary>
    /// Adds peek command services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddPeekServices(this IServiceCollection services)
    {
        services.AddSingleton<IPeekService, PeekService>();
        services.AddSingleton<IMessageRetrievalStrategy, PollingStrategy>();

        return services;
    }

    /// <summary>
    /// Adds purge command services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddPurgeServices(this IServiceCollection services)
    {
        services.AddSingleton<IPurgeService, PurgeService>();
        services.AddSingleton<IPurgeOutputService, PurgeOutputService>();
        return services;
    }

    /// <summary>
    /// Adds all RmqCli services required for publish command to the service collection.
    /// This is a convenience method that calls all required registration methods.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <param name="publishOptions">Publish-specific options.</param>
    /// <param name="outputOptions">Output formatting options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqPublish(
        this IServiceCollection services,
        ParseResult parseResult,
        PublishOptions publishOptions,
        OutputOptions outputOptions)
    {
        services.AddRmqLogging(outputOptions.Verbose);
        services.AddRmqConfiguration(parseResult);

        // Register command-specific options as singletons
        services.AddSingleton(publishOptions);
        services.AddSingleton(outputOptions);

        services.AddRmqCoreServices();
        services.AddPublishServices();

        return services;
    }

    /// <summary>
    /// Adds all RmqCli services required for consume command to the service collection.
    /// This is a convenience method that calls all required registration methods.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <param name="consumeOptions">Consume specific options</param>
    /// <param name="outputOptions">Output formatting options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqConsume(
        this IServiceCollection services,
        ParseResult parseResult,
        MessageRetrievalOptions consumeOptions,
        OutputOptions outputOptions)
    {
        services.AddRmqLogging(outputOptions.Verbose);
        services.AddRmqConfiguration(parseResult);

        // Register command-specific options as singletons
        services.AddSingleton(consumeOptions);
        services.AddSingleton(outputOptions);

        services.AddRmqCoreServices();
        services.AddMessageRetrievalServices();
        services.AddConsumeServices();

        return services;
    }

    /// <summary>
    /// Adds all RmqCli services required for peek command to the service collection.
    /// This is a convenience method that calls all required registration methods.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <param name="peekOptions">Peek-specific options.</param>
    /// <param name="outputOptions">Output formatting options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqPeek(
        this IServiceCollection services,
        ParseResult parseResult,
        MessageRetrievalOptions peekOptions,
        OutputOptions outputOptions)
    {
        services.AddRmqLogging(outputOptions.Verbose);
        services.AddRmqConfiguration(parseResult);

        // Register command-specific options as singletons
        services.AddSingleton(peekOptions);
        services.AddSingleton(outputOptions);

        services.AddRmqCoreServices();
        services.AddMessageRetrievalServices();
        services.AddPeekServices();

        return services;
    }

    /// <summary>
    /// Adds all RmqCli services required for purge command to the service collection.
    /// This is a convenience method that calls all required registration methods.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <param name="purgeOptions">Purge-specific options.</param>
    /// <param name="outputOptions">Output formatting options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqPurge(
        this IServiceCollection services,
        ParseResult parseResult,
        PurgeOptions purgeOptions,
        OutputOptions outputOptions)
    {
        services.AddRmqLogging(outputOptions.Verbose);
        services.AddRmqConfiguration(parseResult);

        // Register command-specific options as singletons
        services.AddSingleton(purgeOptions);
        services.AddSingleton(outputOptions);

        services.AddRmqCoreServices(useManagementClient: true);
        services.AddPurgeServices();

        return services;
    }
}