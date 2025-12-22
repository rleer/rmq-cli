using Microsoft.Extensions.Configuration;
using RmqCli.Infrastructure.Configuration;
using RmqCli.Infrastructure.Configuration.Models;

namespace RmqCli.Integration.Tests.Infrastructure.Configuration;

public class ConfigurationExtensionsTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigurationExtensionsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"rmq-ext-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        // Override user config file path to use temp directory
        Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", Path.Combine(_tempDir, "config.toml"));
    }

    public void Dispose()
    {
        // Clean up temp dir
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up RMQCLI env vars
        Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Host", null);
        Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Port", null);
        Environment.SetEnvironmentVariable("RMQCLI_SYSTEM_CONFIG_PATH", null);
        Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
    }

    [Fact]
    public void CreatesDefaultUserConfig_WhenItDoesNotExist()
    {
        // Arrange
        var expectedConfigPath = TomlConfigurationHelper.GetUserConfigFilePath();

        // Ensure it doesn't exist yet
        if (File.Exists(expectedConfigPath))
        {
            File.Delete(expectedConfigPath);
        }

        // Act
        var builder = new ConfigurationBuilder();
        builder.AddRmqConfig(null);

        // Assert
        File.Exists(expectedConfigPath).Should().BeTrue();
        var content = File.ReadAllText(expectedConfigPath);
        content.Should().Contain("[RabbitMq]");
        content.Should().Contain("Host = \"localhost\"");
    }

    [Fact]
    public void RespectsPriorityOrder()
    {
        // Priority: Env Var > Custom Config > User Config > System Config > Default Config

        // Arrange
        // 1. System Config (Lowest priority of explicit sources)
        var systemConfigPath = Path.Combine(_tempDir, "system.toml");
        Environment.SetEnvironmentVariable("RMQCLI_SYSTEM_CONFIG_PATH", systemConfigPath);
        File.WriteAllText(systemConfigPath, """
                                            [RabbitMq]
                                            Host = "system-host"
                                            Port = 1111
                                            User = "system-user"
                                            VirtualHost = "system-vhost"
                                            Password = "system-password"
                                            """);

        // 2. User Config (Overrides System)
        TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();
        var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath();
        File.WriteAllText(userConfigPath, """
                                          [RabbitMq]
                                          Host = "user-host"
                                          Port = 2222
                                          User = "user-user"
                                          VirtualHost = "user-vhost"
                                          """);

        // 3. Custom Config (Overrides User)
        var customConfigPath = Path.Combine(_tempDir, "custom.toml");
        File.WriteAllText(customConfigPath, """
                                            [RabbitMq]
                                            Host = "custom-host"
                                            Port = 3333
                                            User = "custom-user"
                                            """);

        // 4. Env Var (Overrides Custom)
        Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Host", "env-host");
        Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Port", "4444");

        // Act
        var builder = new ConfigurationBuilder();
        builder.AddRmqConfig(customConfigPath);
        var config = builder.Build();
        var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();

        // Assert
        rabbitMqConfig.Should().NotBeNull();
        rabbitMqConfig.Host.Should().Be("env-host"); // From Env Var (Highest)
        rabbitMqConfig.Port.Should().Be(4444); // From Env Var
        rabbitMqConfig.User.Should().Be("custom-user"); // From Custom Config
        rabbitMqConfig.VirtualHost.Should().Be("user-vhost"); // From User Config
        rabbitMqConfig.Password.Should().Be("system-password"); // From System Config
        rabbitMqConfig.Exchange.Should().Be("amq.direct"); // From Default Config (Implicit fallback)
    }

    [Fact]
    public void PartialConfigurationsMergeCorrectly()
    {
        // Arrange
        // User config has FileConfig
        TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();
        var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath();
        File.WriteAllText(userConfigPath, """
                                          [FileConfig]
                                          MessagesPerFile = 500
                                          """);

        // Custom config has RabbitMq config
        var customConfigPath = Path.Combine(_tempDir, "custom.toml");
        File.WriteAllText(customConfigPath, """
                                            [RabbitMq]
                                            Host = "custom-host"
                                            """);

        // Act
        var builder = new ConfigurationBuilder();
        builder.AddRmqConfig(customConfigPath);
        var config = builder.Build();

        var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();
        var fileConfig = config.GetSection("FileConfig").Get<FileConfig>();

        // Assert
        rabbitMqConfig.Should().NotBeNull();
        rabbitMqConfig.Host.Should().Be("custom-host");

        fileConfig.Should().NotBeNull();
        fileConfig.MessagesPerFile.Should().Be(500);
    }

    [Fact]
    public void EmptyConfigFileUsesClassDefaults()
    {
        // Arrange
        var customConfigPath = Path.Combine(_tempDir, "empty.toml");
        File.WriteAllText(customConfigPath, string.Empty);

        // Act
        var builder = new ConfigurationBuilder();
        builder.AddRmqConfig(customConfigPath);
        var config = builder.Build();
        var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();

        // Assert
        rabbitMqConfig.Should().NotBeNull();
        rabbitMqConfig.Host.Should().Be("localhost"); // Default value
        rabbitMqConfig.Port.Should().Be(5672); // Default value
    }

    [Fact]
    public void MissingCustomConfig_DoesNotThrow()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDir, "nonexistent.toml");

        // Act
        var builder = new ConfigurationBuilder();
        var act = () => builder.AddRmqConfig(nonExistentPath);

        // Assert
        act.Should().NotThrow();

        // Verify it still builds valid config (using defaults/user config)
        var config = builder.Build();
        var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();
        rabbitMqConfig.Should().NotBeNull();
        rabbitMqConfig.Host.Should().Be("localhost");
    }
    
    [Fact]
    public void AllConfigValuesCanBeParsedCorrectly()
    {
        // Arrange
        var customConfigPath = Path.Combine(_tempDir, "full.toml");
        File.WriteAllText(customConfigPath, """
                                            [RabbitMq]
                                            Host = "full-host"
                                            Port = 5671
                                            ManagementPort = 15673
                                            VirtualHost = "full-vhost"
                                            User = "full-user"
                                            Password = "full-password"
                                            Exchange = "full-exchange"
                                            ClientName = "full-client"
                                            UseTls = true
                                            TlsServerName = "tls-server"
                                            TlsAcceptAllCertificates = true
                                            
                                            [FileConfig]
                                            MessagesPerFile = 2000
                                            MessageDelimiter = "\n\n"
                                            """);

        // Act
        var builder = new ConfigurationBuilder();
        builder.AddRmqConfig(customConfigPath);
        var config = builder.Build();
        var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();
        var fileConfig = config.GetSection("FileConfig").Get<FileConfig>();

        // Assert
        rabbitMqConfig.Should().NotBeNull();
        rabbitMqConfig.Host.Should().Be("full-host");
        rabbitMqConfig.Port.Should().Be(5671);
        rabbitMqConfig.ManagementPort.Should().Be(15673);
        rabbitMqConfig.VirtualHost.Should().Be("full-vhost");
        rabbitMqConfig.User.Should().Be("full-user");
        rabbitMqConfig.Password.Should().Be("full-password");
        rabbitMqConfig.Exchange.Should().Be("full-exchange");
        rabbitMqConfig.ClientName.Should().Be("full-client");
        rabbitMqConfig.UseTls.Should().BeTrue();
        rabbitMqConfig.TlsServerName.Should().Be("tls-server");
        rabbitMqConfig.TlsAcceptAllCertificates.Should().BeTrue();

        fileConfig.Should().NotBeNull();
        fileConfig.MessagesPerFile.Should().Be(2000);
        fileConfig.MessageDelimiter.Should().Be("\n\n");
    }
}