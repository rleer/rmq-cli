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

    public static string GetUserConfigFilePath()
    {
        // Allow overriding user config file path via environment variable (useful for testing and containers)
        var envPath = Environment.GetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        return Path.Combine(GetUserConfigDirectory(), "config.toml");
    }

    public static string GetSystemConfigFilePath()
    {
        // Allow overriding system config path via environment variable (useful for testing and containers)
        var envPath = Environment.GetEnvironmentVariable("RMQCLI_SYSTEM_CONFIG_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix or PlatformID.MacOSX => $"/etc/{Constants.AppName}/config.toml",
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Constants.AppName, "config.toml")
        };
    }

    private static void EnsureUserConfigDirectoryExists()
    {
        var configPath = GetUserConfigFilePath();
        var configDir = Path.GetDirectoryName(configPath);

        if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
    }

    /// <summary>
    /// Creates the default user configuration file if it does not already exist.
    /// </summary>
    /// <returns>Indicates whether the default config was created.</returns>
    public static bool CreateDefaultUserConfigIfNotExists()
    {
        var configPath = GetUserConfigFilePath();
        if (!File.Exists(configPath))
        {
            EnsureUserConfigDirectoryExists();

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