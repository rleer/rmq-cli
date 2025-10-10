using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RmqCli.Common;
using RmqCli.Configuration;
using RmqCli.ConsumeCommand;
using RmqCli.ConsumeCommand.MessageFormatter;
using RmqCli.ConsumeCommand.MessageWriter;
using RmqCli.PublishCommand;

namespace RmqCli;

public class ServiceFactory
{
    public IConsumeService CreateConsumeService(ParseResult parseResult)
    {
        var services = new ServiceCollection();
        Bootstrap(parseResult, services);

        services.AddSingleton<IRabbitChannelFactory, RabbitChannelFactory>();
        services.AddSingleton<IStatusOutputService, StatusOutputService>();
        services.AddSingleton<IConsumeService, ConsumeService>();

        // Register message formatters
        services.AddSingleton<IMessageFormatter, TextMessageFormatter>();
        services.AddSingleton<IMessageFormatter, JsonMessageFormatter>();
        services.AddSingleton<IMessageFormatterFactory, MessageFormatterFactory>();

        // Register message writers
        services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
        services.AddSingleton<IMessageWriter, SingleFileMessageWriter>();
        services.AddSingleton<IMessageWriter, RotatingFileMessageWriter>();
        services.AddSingleton<IMessageWriterFactory, MessageWriterFactory>();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IConsumeService>();
    }

    public IPublishService CreatePublishService(ParseResult parseResult)
    {
        var services = new ServiceCollection();
        Bootstrap(parseResult, services);

        services.AddSingleton<IRabbitChannelFactory, RabbitChannelFactory>();
        services.AddSingleton<IStatusOutputService, StatusOutputService>();
        services.AddSingleton<IPublishOutputService, PublishOutputService>();
        services.AddSingleton<IPublishService, PublishService>();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IPublishService>();
    }

    private void Bootstrap(ParseResult parseResult, IServiceCollection services)
    {
        // Get common CLI options
        var verboseLogging = parseResult.GetValue<bool>("--verbose");
        var quietLogging = parseResult.GetValue<bool>("--quiet");
        var format = parseResult.GetValue<OutputFormat>("--output");
        var noColor = parseResult.GetValue<bool>("--no-color");
        var customConfigPath = parseResult.GetValue<string>("--config");

        ConfigureLogging(services, verboseLogging);

        // Build custom configuration
        var configuration = new ConfigurationBuilder()
            .AddRmqConfig(customConfigPath)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        var rabbitMqConfig = new RabbitMqConfig();
        configuration.GetSection(RabbitMqConfig.RabbitMqConfigName).Bind(rabbitMqConfig);
        services.AddSingleton(rabbitMqConfig);

        var fileConfig = new FileConfig();
        configuration.GetSection(nameof(FileConfig)).Bind(fileConfig);
        services.AddSingleton(fileConfig);

        var cliConfig = new CliConfig
        {
            Format = format,
            Quiet = quietLogging,
            Verbose = verboseLogging,
            NoColor = noColor
        };
        services.AddSingleton(cliConfig);
    }

    private void ConfigureLogging(IServiceCollection services, bool verbose)
    {
        var logLevel = verbose ? LogLevel.Debug : LogLevel.None;

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
    }
}