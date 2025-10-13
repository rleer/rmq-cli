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
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constants.AppName)
        };
    }

    public static string GetUserConfigFilePath()
    {
        return Path.Combine(GetUserConfigDirectory(), "config.toml");
    }

    public static string GetSystemConfigFilePath()
    {
        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix or PlatformID.MacOSX => $"/etc/{Constants.AppName}/config.toml",
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constants.AppName, "config.toml")
        };
    }

    private static void EnsureUserConfigDirectoryExists()
    {
        var configDir = GetUserConfigDirectory();
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
    }

    public static void CreateDefaultUserConfigIfNotExists()
    {
        var configPath = GetUserConfigFilePath();
        if (!File.Exists(configPath))
        {
            EnsureUserConfigDirectoryExists();

            var defaultConfig = GenerateDefaultTomlConfig();
            File.WriteAllText(configPath, defaultConfig);
        }
    }

    private static string GenerateDefaultTomlConfig()
    {
        return """
               # rmq Configuration File
               # This file contains default settings for the rmq CLI tool

               [RabbitMq]
               Host = "localhost"
               Port = 5672
               VirtualHost = "/"
               User = "guest"
               Password = "guest"
               Exchange = "amq.direct"
               ClientName = "rmq-cli-tool"

               [FileConfig]
               MessagesPerFile = 10000
               ## Default value is the OS specific newline character
               # MessageDelimiter="\n"
               """;
    }
}