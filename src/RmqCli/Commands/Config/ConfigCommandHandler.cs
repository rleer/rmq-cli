using System.CommandLine;
using RmqCli.Infrastructure.Configuration;
using RmqCli.Shared;

namespace RmqCli.Commands.Config;

public class ConfigCommandHandler : ICommandHandler
{
    public void Configure(RootCommand rootCommand)
    {
        const string description = """
                                   Manage configuration files for the rmq CLI tool.

                                   This command allows you to view, initialize, edit, and reset the configuration files used by rmq.

                                   On the first run, a default configuration file is created in the user's home directory.
                                   The configuration file is in TOML format. All available options are documented in the default configuration file.

                                   Configuration is loaded in the following priority order (highest priority wins):
                                   1. CLI flags
                                   2. Environment variables (prefixed with `RMQCLI_`)
                                   3. Custom config file (via `--config` flag)
                                   4. User config file: `~/.config/rmq/config.toml`
                                   """;

        var configCommand = new Command("config", description);

        // config show - shows the current configuration file path and contents
        var showCommand = new Command("show", "Show current configuration");
        showCommand.SetAction(ShowConfig);
        configCommand.Subcommands.Add(showCommand);

        // config init - initializes a default configuration file
        var initCommand = new Command("init", "Initialize a default configuration file");
        initCommand.SetAction(InitConfig);
        configCommand.Subcommands.Add(initCommand);

        // config path - shows the path to the current configuration file
        var pathCommand = new Command("path", "Show the path to the current configuration file");
        pathCommand.SetAction(ShowConfigPath);
        configCommand.Subcommands.Add(pathCommand);

        // config edit - opens the configuration file in the default editor
        var editCommand = new Command("edit", "Edit the configuration file in the default editor");
        editCommand.SetAction(EditConfig);
        configCommand.Subcommands.Add(editCommand);

        // config reset - resets the configuration file to default
        var resetCommand = new Command("reset", "Reset the user configuration file to default");
        resetCommand.SetAction(ResetConfig);
        configCommand.Subcommands.Add(resetCommand);

        rootCommand.Subcommands.Add(configCommand);
    }

    private static Task<int> ShowConfig(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var quiet = parseResult.GetValue<bool>("--quiet");
        var userConfigPathOverride = parseResult.GetValue<string>("--user-config-path");
        var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath(userConfigPathOverride);

        if (File.Exists(userConfigPath))
        {
            if (!quiet)
            {
                Console.Error.WriteLine($"Current user configuration file: {userConfigPath}");
                Console.Error.WriteLine();
            }

            var userConfig = File.ReadAllText(userConfigPath);
            Console.Out.WriteLine(userConfig);
            return Task.FromResult(0);
        }

        Console.Error.WriteLine(
            $"{Constants.ErrorSymbol} No configuration file found. Run the 'config init' command to create a default configuration file.");
        return Task.FromResult(1);
    }

    private static Task<int> InitConfig(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var userConfigPathOverride = parseResult.GetValue<string>("--user-config-path");
        var configCreated = TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists(userConfigPathOverride);

        if (!configCreated)
        {
            var configPath = TomlConfigurationHelper.GetUserConfigFilePath(userConfigPathOverride);
            Console.Error.WriteLine($"{Constants.WarningSymbol} Config already exists at: {configPath}");
            return Task.FromResult(1);
        }

        var quiet = parseResult.GetValue<bool>("--quiet");
        if (!quiet)
        {
            var configPath = TomlConfigurationHelper.GetUserConfigFilePath(userConfigPathOverride);
            Console.Error.WriteLine($"{Constants.SuccessSymbol} Created default configuration file at: {configPath}");
        }

        return Task.FromResult(0);
    }

    private static Task<int> ShowConfigPath(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var userConfigPathOverride = parseResult.GetValue<string>("--user-config-path");
        var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath(userConfigPathOverride);
        var userConfigExists = File.Exists(userConfigPath);

        if (userConfigExists)
        {
            Console.Out.WriteLine($"User configuration file path: {userConfigPath}");
            return Task.FromResult(0);
        }

        Console.Error.WriteLine(
            $"{Constants.ErrorSymbol} No configuration file found. Run the 'config init' command to create a default configuration file.");
        return Task.FromResult(1);
    }

    private static Task<int> EditConfig(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var userConfigPathOverride = parseResult.GetValue<string>("--user-config-path");
        var configPath = TomlConfigurationHelper.GetUserConfigFilePath(userConfigPathOverride);
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"{Constants.ErrorSymbol} No configuration file found. Run 'config init' to create a default configuration file.");
            return Task.FromResult(1);
        }

        try
        {
            // Open the configuration file in the default editor
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });
            Console.Error.WriteLine($"{Constants.SuccessSymbol} Opened configuration file in the default editor: {configPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{Constants.ErrorSymbol} Failed to open configuration file: {ex.Message}");
            Console.Error.WriteLine($"  Please manually edit the configuration file at: {configPath}");
            return Task.FromResult(1);
        }

        return Task.FromResult(0);
    }

    private static Task<int> ResetConfig(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var userConfigPathOverride = parseResult.GetValue<string>("--user-config-path");
        var configPath = TomlConfigurationHelper.GetUserConfigFilePath(userConfigPathOverride);
        if (File.Exists(configPath))
        {
            try
            {
                File.Delete(configPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{Constants.ErrorSymbol} Failed to delete previous configuration file: {ex.Message}");
                return Task.FromResult(1);
            }
        }

        var quiet = parseResult.GetValue<bool>("--quiet");
        var created = TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists(userConfigPathOverride);

        if (!created)
        {
            Console.Error.WriteLine($"{Constants.ErrorSymbol} Failed to create default configuration file at: {configPath}");
            return Task.FromResult(1);
        }

        if (!quiet && created)
        {
            Console.Error.WriteLine($"Configuration reset to defaults: {configPath}");
        }

        return Task.FromResult(0);
    }
}