using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RmqCli.Commands.Consume;
using RmqCli.Commands.Publish;
using RmqCli.Core.Services;
using RmqCli.Infrastructure.Configuration;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output;
using RmqCli.Shared;

namespace RmqCli.DependencyInjection;

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
    public static IServiceCollection AddRmqConfiguration(this IServiceCollection services, ParseResult parseResult)
    {
        var customConfigPath = parseResult.GetValue<string>("--config");

        // Build configuration from TOML files
        var configuration = new ConfigurationBuilder()
            .AddRmqConfig(customConfigPath)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Bind and register RabbitMQ configuration
        var rabbitMqConfig = new RabbitMqConfig();
        configuration.GetSection(RabbitMqConfig.RabbitMqConfigName).Bind(rabbitMqConfig);
        services.AddSingleton(rabbitMqConfig);

        // Bind and register file configuration
        var fileConfig = new FileConfig();
        configuration.GetSection(nameof(FileConfig)).Bind(fileConfig);
        services.AddSingleton(fileConfig);

        return services;
    }

    /// <summary>
    /// Adds RmqCli CLI configuration from parse result to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqCliConfig(this IServiceCollection services, ParseResult parseResult)
    {
        var verboseLogging = parseResult.GetValue<bool>("--verbose");
        var quietLogging = parseResult.GetValue<bool>("--quiet");
        var format = parseResult.GetValue<OutputFormat>("--output");
        var noColor = parseResult.GetValue<bool>("--no-color");

        var cliConfig = new CliConfig
        {
            Format = format,
            Quiet = quietLogging,
            Verbose = verboseLogging,
            NoColor = noColor
        };

        services.AddSingleton(cliConfig);
        return services;
    }

    /// <summary>
    /// Adds RmqCli logging configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="verbose">Whether to enable verbose (Debug level) logging.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqLogging(this IServiceCollection services, bool verbose)
    {
        var logLevel = verbose ? LogLevel.Debug : LogLevel.None;

        services.AddLogging(builder =>
        {
            builder.AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                })
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
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitChannelFactory, RabbitChannelFactory>();
        services.AddSingleton<IStatusOutputService, StatusOutputService>();

        return services;
    }

    /// <summary>
    /// Adds publish command services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPublishServices(this IServiceCollection services)
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
    public static IServiceCollection AddConsumeServices(this IServiceCollection services)
    {
        services.AddSingleton<IConsumeService, ConsumeService>();

        return services;
    }

    /// <summary>
    /// Adds all RmqCli services required for publish command to the service collection.
    /// This is a convenience method that calls all required registration methods.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="parseResult">The parse result containing CLI options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqPublish(this IServiceCollection services, ParseResult parseResult)
    {
        var verboseLogging = parseResult.GetValue<bool>("--verbose");

        services.AddRmqLogging(verboseLogging);
        services.AddRmqConfiguration(parseResult);
        services.AddRmqCliConfig(parseResult);
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
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRmqConsume(this IServiceCollection services, ParseResult parseResult)
    {
        var verboseLogging = parseResult.GetValue<bool>("--verbose");

        services.AddRmqLogging(verboseLogging);
        services.AddRmqConfiguration(parseResult);
        services.AddRmqCliConfig(parseResult);
        services.AddRmqCoreServices();
        services.AddConsumeServices();

        return services;
    }
}
