using Microsoft.Extensions.Configuration;
using RmqCli.Infrastructure.Configuration;
using RmqCli.Infrastructure.Configuration.Models;

namespace RmqCli.Integration.Tests.Infrastructure.Configuration;

public class ConfigurationPriorityTests
{
    #region ConfigurationPriority

    public class ConfigurationPriority : IDisposable
    {
        private readonly string _tempDir;

        public ConfigurationPriority()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"rmq-priority-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }

            // Clean up environment variables
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Host", null);
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Port", null);
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__User", null);
            Environment.SetEnvironmentVariable("RMQCLI_FileConfig__MessagesPerFile", null);
        }

        [Fact]
        public void CustomConfigOverridesUserConfig()
        {
            // Arrange
            var userConfigPath = Path.Combine(_tempDir, "user-config.toml");
            var customConfigPath = Path.Combine(_tempDir, "custom-config.toml");

            File.WriteAllText(userConfigPath, """
                [RabbitMq]
                Host = "user-host"
                Port = 5678
                """);

            File.WriteAllText(customConfigPath, """
                [RabbitMq]
                Host = "custom-host"
                """);

            // Act
            var config = new ConfigurationBuilder()
                .AddTomlConfig(userConfigPath)
                .AddTomlConfig(customConfigPath)
                .Build();

            var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();

            // Assert
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("custom-host");
            rabbitMqConfig.Port.Should().Be(5678); // From user config
        }

        [Fact]
        public void EnvironmentVariablesOverrideAllConfigs()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            File.WriteAllText(configPath, """
                [RabbitMq]
                Host = "config-host"
                Port = 5672
                User = "config-user"
                """);

            // Set environment variables
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Host", "env-host");
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__User", "env-user");

            // Act
            var config = new ConfigurationBuilder()
                .AddTomlConfig(configPath)
                .AddEnvironmentVariables("RMQCLI_")
                .Build();

            var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();

            // Assert
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("env-host"); // From environment
            rabbitMqConfig.User.Should().Be("env-user"); // From environment
            rabbitMqConfig.Port.Should().Be(5672); // From config file
        }

        [Fact]
        public void EnvironmentVariableUsesDunderNotation()
        {
            // Arrange
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Host", "env-host");
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Port", "6000");
            Environment.SetEnvironmentVariable("RMQCLI_FileConfig__MessagesPerFile", "5000");

            // Act
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("RMQCLI_")
                .Build();

            var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();
            var fileConfig = config.GetSection("FileConfig").Get<FileConfig>();

            // Assert
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("env-host");
            rabbitMqConfig.Port.Should().Be(6000);
            fileConfig.Should().NotBeNull();
            fileConfig.MessagesPerFile.Should().Be(5000);
        }

        [Fact]
        public void LaterSourcesOverrideEarlierSources()
        {
            // Arrange
            var config1Path = Path.Combine(_tempDir, "config1.toml");
            var config2Path = Path.Combine(_tempDir, "config2.toml");
            var config3Path = Path.Combine(_tempDir, "config3.toml");

            File.WriteAllText(config1Path, """
                [RabbitMq]
                Host = "host1"
                Port = 5672
                User = "user1"
                """);

            File.WriteAllText(config2Path, """
                [RabbitMq]
                Host = "host2"
                User = "user2"
                """);

            File.WriteAllText(config3Path, """
                [RabbitMq]
                Host = "host3"
                """);

            // Act
            var config = new ConfigurationBuilder()
                .AddTomlConfig(config1Path)
                .AddTomlConfig(config2Path)
                .AddTomlConfig(config3Path)
                .Build();

            var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();

            // Assert
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("host3"); // From config3
            rabbitMqConfig.User.Should().Be("user2"); // From config2
            rabbitMqConfig.Port.Should().Be(5672); // From config1
        }

        [Fact]
        public void MissingConfigFileDoesNotAffectPriority()
        {
            // Arrange
            var existingConfigPath = Path.Combine(_tempDir, "existing.toml");
            var missingConfigPath = Path.Combine(_tempDir, "missing.toml");

            File.WriteAllText(existingConfigPath, """
                [RabbitMq]
                Host = "existing-host"
                Port = 5672
                """);

            // Don't create missing config

            // Act
            var config = new ConfigurationBuilder()
                .AddTomlConfig(existingConfigPath)
                .AddTomlConfig(missingConfigPath) // Should be ignored
                .Build();

            var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();

            // Assert
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("existing-host");
            rabbitMqConfig.Port.Should().Be(5672);
        }

        [Fact]
        public void PartialConfigurationsMergeCorrectly()
        {
            // Arrange
            var baseConfigPath = Path.Combine(_tempDir, "base.toml");
            var overrideConfigPath = Path.Combine(_tempDir, "override.toml");

            File.WriteAllText(baseConfigPath, """
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
                """);

            File.WriteAllText(overrideConfigPath, """
                [RabbitMq]
                Host = "production-host"
                User = "admin"
                """);

            // Act
            var config = new ConfigurationBuilder()
                .AddTomlConfig(baseConfigPath)
                .AddTomlConfig(overrideConfigPath)
                .Build();

            var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();
            var fileConfig = config.GetSection("FileConfig").Get<FileConfig>();

            // Assert
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("production-host"); // Overridden
            rabbitMqConfig.User.Should().Be("admin"); // Overridden
            rabbitMqConfig.Port.Should().Be(5672); // From base
            rabbitMqConfig.VirtualHost.Should().Be("/"); // From base
            rabbitMqConfig.Password.Should().Be("guest"); // From base
            rabbitMqConfig.Exchange.Should().Be("amq.direct"); // From base
            rabbitMqConfig.ClientName.Should().Be("rmq-cli-tool"); // From base

            fileConfig.Should().NotBeNull();
            fileConfig.MessagesPerFile.Should().Be(10000); // From base
        }

        [Fact]
        public void EnvironmentVariableOverridesSingleProperty()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            File.WriteAllText(configPath, """
                [RabbitMq]
                Host = "localhost"
                Port = 5672
                User = "guest"
                Password = "guest"
                """);

            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Password", "secret-password");

            // Act
            var config = new ConfigurationBuilder()
                .AddTomlConfig(configPath)
                .AddEnvironmentVariables("RMQCLI_")
                .Build();

            var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();

            // Assert
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("localhost");
            rabbitMqConfig.Port.Should().Be(5672);
            rabbitMqConfig.User.Should().Be("guest");
            rabbitMqConfig.Password.Should().Be("secret-password"); // Overridden by env var
        }

        [Fact]
        public void EmptyConfigFileUsesClassDefaults()
        {
            // Arrange
            var emptyConfigPath = Path.Combine(_tempDir, "empty.toml");
            File.WriteAllText(emptyConfigPath, string.Empty);

            // Act
            var config = new ConfigurationBuilder()
                .AddTomlConfig(emptyConfigPath)
                .Build();

            // Bind to a new instance to get default values from the class
            var rabbitMqConfig = new RabbitMqConfig();
            config.GetSection(RabbitMqConfig.RabbitMqConfigName).Bind(rabbitMqConfig);

            // Assert - Should get default values from RabbitMqConfig class
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("localhost");
            rabbitMqConfig.Port.Should().Be(5672);
        }

        [Fact]
        public void MultipleEnvironmentVariablesWork()
        {
            // Arrange
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Host", "env-host");
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__Port", "7000");
            Environment.SetEnvironmentVariable("RMQCLI_RabbitMq__User", "env-user");
            Environment.SetEnvironmentVariable("RMQCLI_FileConfig__MessagesPerFile", "8000");

            // Act
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("RMQCLI_")
                .Build();

            var rabbitMqConfig = config.GetSection(RabbitMqConfig.RabbitMqConfigName).Get<RabbitMqConfig>();
            var fileConfig = config.GetSection("FileConfig").Get<FileConfig>();

            // Assert
            rabbitMqConfig.Should().NotBeNull();
            rabbitMqConfig.Host.Should().Be("env-host");
            rabbitMqConfig.Port.Should().Be(7000);
            rabbitMqConfig.User.Should().Be("env-user");
            fileConfig.Should().NotBeNull();
            fileConfig.MessagesPerFile.Should().Be(8000);
        }
    }

    #endregion
}
