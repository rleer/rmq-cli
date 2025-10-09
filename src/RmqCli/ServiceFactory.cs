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
    private readonly IServiceCollection _services = new ServiceCollection();

    public IConsumeService CreateConsumeService(ParseResult parseResult)
    {
        Bootstrap(parseResult);

        _services.AddSingleton<IRabbitChannelFactory, RabbitChannelFactory>();
        _services.AddSingleton<IStatusOutputService, StatusOutputService>();
        _services.AddSingleton<IConsumeService, ConsumeService>();

        // Register message formatters
        _services.AddSingleton<IMessageFormatter, TextMessageFormatter>();
        _services.AddSingleton<IMessageFormatter, JsonMessageFormatter>();
        _services.AddSingleton<IMessageFormatterFactory, MessageFormatterFactory>();

        // Register message writers
        _services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
        _services.AddSingleton<IMessageWriter, SingleFileMessageWriter>();
        _services.AddSingleton<IMessageWriter, RotatingFileMessageWriter>();
        _services.AddSingleton<IMessageWriterFactory, MessageWriterFactory>();

        var serviceProvider = _services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IConsumeService>();
    }

    public IPublishService CreatePublishService(ParseResult parseResult)
    {
        Bootstrap(parseResult);

        _services.AddSingleton<IRabbitChannelFactory, RabbitChannelFactory>();
        _services.AddSingleton<IStatusOutputService, StatusOutputService>();
        _services.AddSingleton<IPublishOutputService, PublishOutputService>();
        _services.AddSingleton<IPublishService, PublishService>();

        var serviceProvider = _services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IPublishService>();
    }

    private void Bootstrap(ParseResult parseResult)
    {
        // Get common CLI options
        var verboseLogging = parseResult.GetValue<bool>("--verbose");
        var quietLogging = parseResult.GetValue<bool>("--quiet");
        var format = parseResult.GetValue<OutputFormat>("--output");
        var noColor = parseResult.GetValue<bool>("--no-color");
        var customConfigPath = parseResult.GetValue<string>("--config");

        ConfigureLogging(verboseLogging);

        // Build custom configuration
        var configuration = new ConfigurationBuilder()
            .AddRmqConfig(customConfigPath)
            .Build();

        _services.AddSingleton<IConfiguration>(configuration);

        var rabbitMqConfig = new RabbitMqConfig();
        configuration.GetSection(RabbitMqConfig.RabbitMqConfigName).Bind(rabbitMqConfig);
        _services.AddSingleton(rabbitMqConfig);

        var fileConfig = new FileConfig();
        configuration.GetSection(nameof(FileConfig)).Bind(fileConfig);
        _services.AddSingleton(fileConfig);

        var cliConfig = new CliConfig
        {
            Format = format,
            Quiet = quietLogging,
            Verbose = verboseLogging,
            NoColor = noColor
        };
        _services.AddSingleton(cliConfig);
    }

    private void ConfigureLogging(bool verbose)
    {
        var logLevel = verbose ? LogLevel.Debug : LogLevel.None;

        _services.AddLogging(builder =>
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