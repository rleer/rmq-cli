using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RmqCli.Commandhandler;
using RmqCli.CommandHandler;
using RmqCli.Common;
using RmqCli.Configuration;
using RmqCli.ConsumeCommand;
using RmqCli.ConsumeCommand.MessageFormatter;
using RmqCli.ConsumeCommand.MessageWriter;
using RmqCli.PublishCommand;
using Spectre.Console;
using AnsiConsoleFactory = RmqCli.Common.AnsiConsoleFactory;

// Create a minimal root command to parse global options first
var rootCommandHandler = new RootCommandHandler();

var (cliConfig, configPath) = rootCommandHandler.ParseGlobalOptions(args);

// Build custom configuration
var configuration = new ConfigurationBuilder()
    .AddRmqConfig(configPath)
    .Build();

// Create service collection for manual DI
var services = new ServiceCollection();

// Add logging only if verbose mode is enabled
if (cliConfig.Verbose)
{
    services.AddLogging(builder =>
    {
        builder.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; })
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddFilter("System", LogLevel.Warning)
            .SetMinimumLevel(LogLevel.Debug);

        builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
            options.IncludeScopes = false;
        });
    });
}
else
{
    // Add minimal logging infrastructure even when not verbose
    services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.None));
}

// Register configuration as singleton
services.AddSingleton<IConfiguration>(configuration);

// Bind and register configuration settings
var rabbitMqConfig = new RabbitMqConfig();
configuration.GetSection(RabbitMqConfig.RabbitMqConfigName).Bind(rabbitMqConfig);
services.AddSingleton(rabbitMqConfig);

var fileConfig = new FileConfig();
configuration.GetSection(nameof(FileConfig)).Bind(fileConfig);
services.AddSingleton(fileConfig);

services.AddSingleton(cliConfig);

// Register services in the DI container
services.AddSingleton<IRabbitChannelFactory, RabbitChannelFactory>();
services.AddSingleton<IPublishService, PublishService>();
services.AddSingleton<IConsumeService, ConsumeService>();
services.AddSingleton<IStatusOutputService, StatusOutputService>();
services.AddSingleton<IPublishOutputService, PublishOutputService>();
services.AddSingleton<IAnsiConsoleFactory, AnsiConsoleFactory>();

// Register message formatters
services.AddSingleton<IMessageFormatter, TextMessageFormatter>();
services.AddSingleton<IMessageFormatter, JsonMessageFormatter>();
services.AddSingleton<IMessageFormatterFactory, MessageFormatterFactory>();

// Register message writers
services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
services.AddSingleton<IMessageWriter, SingleFileMessageWriter>();
services.AddSingleton<IMessageWriter, RotatingFileMessageWriter>();
services.AddSingleton<IMessageWriterFactory, MessageWriterFactory>();

// Register command handlers
services.AddSingleton<ICommandHandler, PublishCommandHandler>();
services.AddSingleton<ICommandHandler, ConsumeCommandHandler>();
services.AddSingleton<ICommandHandler, ConfigCommandHandler>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

try
{
    // Configure the root command with all command handlers
    rootCommandHandler.ConfigureCommands(serviceProvider);

    // Run the command line application
    var exitCode = rootCommandHandler.RunAsync(args);

    return exitCode;
}
catch (Exception e)
{
    AnsiConsole.MarkupLineInterpolated($"[indianred1]⚠ An error occurred: {e.Message}[/]");
    logger.LogError(e, "Application terminated unexpectedly");
    return 1;
}