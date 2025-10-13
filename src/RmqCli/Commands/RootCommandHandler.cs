using System.CommandLine;
using RmqCli.Commands.Config;
using RmqCli.Commands.Consume;
using RmqCli.Commands.Publish;
using RmqCli.Shared;

namespace RmqCli.Commands;

public class RootCommandHandler
{
    private readonly ServiceFactory _serviceFactory;
    private readonly RootCommand _rootCommand;
    private readonly List<ICommandHandler> _commandHandlers = [];

    private const string RabbitAscii = """
                                         (\(\
                                         (-.-)
                                         o(")(")
                                       """;

    public RootCommandHandler(ServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
        _rootCommand = new RootCommand($"{RabbitAscii}\nDeveloper focused utility tool for common RabbitMQ tasks");
        
        // Setup global options
        SetupGlobalOptions();
        
        // Setup command handlers
        SetupCommands();
    }
    
    private void SetupGlobalOptions()
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
    }

    private void SetupCommands()
    {
        _commandHandlers.Add(new ConfigCommandHandler());
        _commandHandlers.Add(new ConsumeCommandHandler(_serviceFactory));
        _commandHandlers.Add(new PublishCommandHandler(_serviceFactory));
        
        foreach (var handler in _commandHandlers)
        {
            handler.Configure(_rootCommand);
        }
    }

    public async Task<int> RunAsync(string[] args)
    {
        var parseResult = _rootCommand.Parse(args);
        return await parseResult.InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
    }
}