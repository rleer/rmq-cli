using System.CommandLine;
using RmqCli.Commands.Config;
using RmqCli.Commands.Consume;
using RmqCli.Commands.Peek;
using RmqCli.Commands.Publish;
using RmqCli.Commands.Purge;
using RmqCli.Shared.Factories;

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

        var userConfigPathOption = new Option<string>("--user-config-path")
        {
            Description = "Override the user configuration file path (useful for testing and containers)",
            Recursive = true
        };
        _rootCommand.Add(userConfigPathOption);

        var vHostOption = new Option<string>("--vhost")
        {
            Description = "The RabbitMQ virtual host to use for this command. Overrides the value specified in the configuration file.",
            Recursive = true
        };
        _rootCommand.Add(vHostOption);
        
        var hostOption = new Option<string>("--host")
        {
            Description = "The RabbitMQ host to use for this command. Overrides the value specified in the configuration file.",
            Recursive = true
        };
        _rootCommand.Add(hostOption);
        
        var portOption = new Option<int>("--port")
        {
            Description = "The RabbitMQ port to use for this command. Overrides the value specified in the configuration file.",
            Recursive = true
        };
        _rootCommand.Add(portOption);
        
        var managementPortOption = new Option<int>("--management-port")
        {
            Description = "The RabbitMQ management API port to use for this command. Overrides the value specified in the configuration file.",
            Recursive = true
        };
        _rootCommand.Add(managementPortOption);
        
        var userOption = new Option<string>("--user")
        {
            Description = "The RabbitMQ user to use for this command. Overrides the value specified in the configuration file.",
            Recursive = true
        };
        _rootCommand.Add(userOption);
        
        var passwordOption = new Option<string>("--password")
        {
            Description = "The RabbitMQ password to use for this command. Overrides the value specified in the configuration file.",
            Recursive = true
        };
        _rootCommand.Add(passwordOption);

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
        _commandHandlers.Add(new PeekCommandHandler(_serviceFactory));
        _commandHandlers.Add(new PublishCommandHandler(_serviceFactory));
        _commandHandlers.Add(new PurgeCommandHandler(_serviceFactory));

        foreach (var handler in _commandHandlers)
        {
            handler.Configure(_rootCommand);
        }
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var parseResult = _rootCommand.Parse(args);
        return await parseResult.InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false }, cancellationToken);
    }
}