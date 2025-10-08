using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using RmqCli.CommandHandler;
using RmqCli.Common;
using RmqCli.Configuration;

namespace RmqCli.Commandhandler;

public class RootCommandHandler
{
    private readonly RootCommand _rootCommand;

    private const string RabbitAscii = """
                                         (\(\
                                         (-.-)
                                         o(")(")
                                       """;

    public RootCommandHandler()
    {
        _rootCommand = new RootCommand($"{RabbitAscii}\nDeveloper focused utility tool for common RabbitMQ tasks");
    }

    public (CliConfig cliConfg, string configPath) ParseGlobalOptions(string[] args)
    {
        var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");
        verboseOption.SetDefaultValue(false);
        _rootCommand.AddGlobalOption(verboseOption);

        var quietOption = new Option<bool>("--quiet", "Minimal output (errors only)");
        quietOption.SetDefaultValue(false);
        _rootCommand.AddGlobalOption(quietOption);

        var outputFormatOption = new Option<OutputFormat>("--output", "Output format. One of: plain, table or json.");
        outputFormatOption.AddAlias("-o");
        outputFormatOption.SetDefaultValue(OutputFormat.Plain);
        _rootCommand.AddGlobalOption(outputFormatOption);

        var noColorOption = new Option<bool>("--no-color", "Disable colored output for dumb terminals");
        noColorOption.SetDefaultValue(false);
        _rootCommand.AddGlobalOption(noColorOption);

        var configFileOption = new Option<string>("--config", "Path to the configuration file (TOML format)");
        _rootCommand.AddGlobalOption(configFileOption);
        
        _rootCommand.AddValidator(result =>
        {
            if (result.GetValueForOption(verboseOption) && result.GetValueForOption(quietOption))
            {
                result.ErrorMessage = "You cannot use both --verbose and --quiet options together.";
            }
        });
        
        // Parse the global options first to set up the environment
        var parseResult = _rootCommand.Parse(args);

        var verboseLogging = parseResult.GetValueForOption(verboseOption);
        var quietLogging = parseResult.GetValueForOption(quietOption);
        var format = parseResult.GetValueForOption(outputFormatOption);
        var noColor = parseResult.GetValueForOption(noColorOption);
        var customConfigPath = parseResult.GetValueForOption(configFileOption);
        
        var cliConfig = new CliConfig
        {
            Format = format,
            Quiet = quietLogging,
            Verbose = verboseLogging,
            NoColor = noColor
        };

        return (cliConfig, customConfigPath ?? string.Empty);
    }

    public void ConfigureCommands(IServiceProvider serviceProvider)
    {
        var commands = serviceProvider.GetServices<ICommandHandler>();
        foreach (var command in commands)
        {
            command.Configure(_rootCommand);
        }
    }

    public int RunAsync(string[] args)
    {
        return _rootCommand.Invoke(args);
    }
}