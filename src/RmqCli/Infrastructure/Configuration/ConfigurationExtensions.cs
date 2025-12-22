using Microsoft.Extensions.Configuration;
using RmqCli.Shared;

namespace RmqCli.Infrastructure.Configuration;

public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddRmqConfig(this IConfigurationBuilder builder, string? customConfigPath)
    {
        // Build configuration in priority order (lowest to highest priority):
        // 1. Default TOML config (fallback)
        // 2. User TOML config
        // 3. Custom TOML config (if specified via --config)
        // 4. Environment variables
        // Create default user config if it does not exist
        TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();

        // Add TOML configuration sources in priority order
        var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath();
        if (File.Exists(userConfigPath))
        {
            builder.AddTomlConfig(userConfigPath);
        }

        // Add custom configuration file if specified
        if (File.Exists(customConfigPath))
        {
            builder.AddTomlConfig(customConfigPath);
        }
        else if (!string.IsNullOrEmpty(customConfigPath))
        {
            Console.Error.WriteLine($"{Constants.WarningSymbol} Custom configuration file not found at '{customConfigPath}'. Falling back to other configuration sources.");
        }

        // Add environment variables as the highest priority configuration source
        builder.AddEnvironmentVariables("RMQCLI_");

        return builder;
    }
}