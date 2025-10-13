using System.CommandLine;
using RmqCli.Infrastructure.Configuration;

namespace RmqCli.Commands.Config;

public class ConfigCommandHandler : ICommandHandler
{
    public void Configure(RootCommand rootCommand)
    {
        const string description = """
                                   Manage configuration files for the rmq CLI tool.

                                   This command allows you to view, initialize, edit, and reset the configuration files used by RmqCli.

                                   On the first run, a default configuration file is created in the user's home directory.
                                   The configuration file is in TOML format. All available options are documented in the default configuration file.

                                   Example usage:
                                     rmq config show
                                     rmq config init
                                     rmq config path
                                     rmq config edit
                                     rmq config reset
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
        var resetCommand = new Command("reset", "Reset the configuration file to default");
        resetCommand.SetAction(ResetConfig);
        configCommand.Subcommands.Add(resetCommand);

        rootCommand.Subcommands.Add(configCommand);
    }

    private static Task ShowConfig(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath();
        var systemConfigPath = TomlConfigurationHelper.GetSystemConfigFilePath();
        var configFound = false;

        if (File.Exists(userConfigPath))
        {
            Console.WriteLine($"Current user configuration file: {userConfigPath}");
            Console.WriteLine();
            var userConfig = File.ReadAllText(userConfigPath);
            Console.WriteLine(userConfig);
            configFound = true;
        }

        if (File.Exists(systemConfigPath))
        {
            Console.WriteLine($"Current system-wide configuration file: {systemConfigPath}");
            Console.WriteLine();
            var systemConfig = File.ReadAllText(systemConfigPath);
            Console.WriteLine(systemConfig);
            configFound = true;
        }

        if (!configFound)
        {
            Console.Error.WriteLine("No configuration file found. Run the 'config init' command to create a default configuration file.");
        }

        return Task.CompletedTask;
    }

    private static Task InitConfig(ParseResult parseResult, CancellationToken cancellationToken)
    {
        TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();
        return Task.CompletedTask;
    }

    private static Task ShowConfigPath(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (File.Exists(TomlConfigurationHelper.GetUserConfigFilePath()))
        {
            Console.WriteLine($"User configuration file path: {TomlConfigurationHelper.GetUserConfigFilePath()}");
        }
        else if (File.Exists(TomlConfigurationHelper.GetSystemConfigFilePath()))
        {
            Console.WriteLine($"System-wide configuration file path: {TomlConfigurationHelper.GetSystemConfigFilePath()}");
        }
        else
        {
            Console.WriteLine("No configuration file found. Run the 'config init' command to create a default configuration file.");
        }
        
        return Task.CompletedTask;
    }

    private static Task EditConfig(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configPath = TomlConfigurationHelper.GetUserConfigFilePath();
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("Configuration file does not exist. Run 'config init' to create a default configuration file.");
            return Task.CompletedTask;
        }

        try
        {
            // Open the configuration file in the default editor
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });
            Console.WriteLine($"Opened configuration file in the default editor: {configPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open configuration file: {ex.Message}");
            Console.Error.WriteLine($"Please manually edit the configuration file at: {configPath}");
        }
        
        return Task.CompletedTask;
    }

    private static Task ResetConfig(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configPath = TomlConfigurationHelper.GetUserConfigFilePath();
        if (File.Exists(configPath))
        {
            try
            {
                File.Delete(configPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to delete previous configuration file: {ex.Message}");
            }
        }

        TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();
        Console.WriteLine($"Configuration reset to defaults: {configPath}");

        return Task.CompletedTask;
    }
}