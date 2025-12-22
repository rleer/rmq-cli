using RmqCli.Shared;

namespace RmqCli.Infrastructure.Configuration;

public class TomlConfigurationHelper
{
    private static string GetUserConfigDirectory()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix or PlatformID.MacOSX => Path.Combine(homeDir, ".config", Constants.AppName),
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Constants.AppName)
        };
    }

    public static string GetUserConfigFilePath(string? overridePath = null)
    {
        // Priority order:
        // 1. Explicit override path (from CLI flag)
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        // 2. Environment variable (useful for testing and containers)
        var envPath = Environment.GetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        // 3. Default location
        return Path.Combine(GetUserConfigDirectory(), "config.toml");
    }

    private static void EnsureUserConfigDirectoryExists(string? overridePath = null)
    {
        var configPath = GetUserConfigFilePath(overridePath);
        var configDir = Path.GetDirectoryName(configPath);

        if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
    }

    /// <summary>
    /// Creates the default user configuration file if it does not already exist.
    /// </summary>
    /// <param name="overridePath">Optional override path for the user config file.</param>
    /// <returns>Indicates whether the default config was created.</returns>
    public static bool CreateDefaultUserConfigIfNotExists(string? overridePath = null)
    {
        var configPath = GetUserConfigFilePath(overridePath);
        if (!File.Exists(configPath))
        {
            EnsureUserConfigDirectoryExists(overridePath);

            var defaultConfig = GenerateDefaultTomlConfig();
            File.WriteAllText(configPath, defaultConfig);
            return true;
        }

        return false;
    }

    private static string GenerateDefaultTomlConfig()
    {
        return """
               # rmq Configuration File
               # This file contains default settings for the rmq CLI tool
               [RabbitMq]
               Host = "localhost"
               Port = 5672
               ManagementPort = 15672
               VirtualHost = "/"
               User = "guest"
               Password = "guest"
               Exchange = "amq.direct"
               ClientName = "rmq-cli-tool"
               UseTls = false
               ## Optional: defaults to Host if not specified
               # TlsServerName = "amqp.example.com"
               ## Optional: set to true to accept self-signed certificates (NOT recommended for production)
               # TlsAcceptAllCertificates = false   
                
               [FileConfig]
               ## Number of messages to write per file before rotating
               MessagesPerFile = 10000
               ## Default value is the OS specific newline character
               # MessageDelimiter="\n"
               """;
    }
}