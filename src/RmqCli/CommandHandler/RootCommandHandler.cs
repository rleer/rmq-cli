using System.CommandLine;
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
        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable verbose logging",
            DefaultValueFactory = _ => false,
            Recursive = true
        };
        _rootCommand.Options.Add(verboseOption);

        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Minimal output (errors only)",
            DefaultValueFactory = _ => false,
            Recursive = true
        };
        _rootCommand.Options.Add(quietOption);

        var outputFormatOption = new Option<OutputFormat>("--output")
        {
            Description = "Output format",
            Aliases = { "-o" },
            DefaultValueFactory = _ => OutputFormat.Plain,
            Recursive = true
        };
        outputFormatOption.AcceptOnlyFromAmong("plain", "table", "json");
        _rootCommand.Options.Add(outputFormatOption);

        var noColorOption = new Option<bool>("--no-color")
        {
            Description = "Disable colored output for dumb terminals",
            DefaultValueFactory = _ => false,
            Recursive = true
        };
        _rootCommand.Options.Add(noColorOption);

        var configFileOption = new Option<string>("--config")
        {
            Description = "Path to configuration file (TOML format). If not specified, the default config file path will be used.",
            Recursive = true
        };
        _rootCommand.Add(configFileOption);
        
        _rootCommand.Validators.Add(result =>
        {
            if (result.GetValue(verboseOption) && result.GetValue(quietOption))
            {
                result.AddError("You cannot use both --verbose and --quiet options together.");
            }
        });
        
        // Parse the global options first to set up the environment
        var parseResult = _rootCommand.Parse(args);

        var verboseLogging = parseResult.GetValue(verboseOption);
        var quietLogging = parseResult.GetValue(quietOption);
        var format = parseResult.GetValue(outputFormatOption);
        var noColor = parseResult.GetValue(noColorOption);
        var customConfigPath = parseResult.GetValue(configFileOption);
        
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
        var parseResult = _rootCommand.Parse(args);
        return parseResult.Invoke();
    }
}