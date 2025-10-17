using Microsoft.Extensions.Configuration;
using RmqCli.Infrastructure.Configuration;

namespace RmqCli.Integration.Tests.Infrastructure.Configuration;

public class TomlConfigurationProviderTests
{
    #region Load

    public class Load : IDisposable
    {
        private readonly string _tempDir;

        public Load()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"rmq-config-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public void LoadsSimpleTomlFile()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            var tomlContent = """
                [RabbitMq]
                Host = "localhost"
                Port = 5672
                """;
            File.WriteAllText(configPath, tomlContent);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert
            provider.TryGet("RabbitMq:Host", out var host).Should().BeTrue();
            host.Should().Be("localhost");
            provider.TryGet("RabbitMq:Port", out var port).Should().BeTrue();
            port.Should().Be("5672");
        }

        [Fact]
        public void LoadsNestedTomlStructure()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            var tomlContent = """
                [RabbitMq]
                Host = "localhost"
                Port = 5672

                [FileConfig]
                MessagesPerFile = 10000
                MessageDelimiter = "\n"
                """;
            File.WriteAllText(configPath, tomlContent);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert
            provider.TryGet("RabbitMq:Host", out var host).Should().BeTrue();
            host.Should().Be("localhost");
            provider.TryGet("RabbitMq:Port", out var port).Should().BeTrue();
            port.Should().Be("5672");
            provider.TryGet("FileConfig:MessagesPerFile", out var messagesPerFile).Should().BeTrue();
            messagesPerFile.Should().Be("10000");
            provider.TryGet("FileConfig:MessageDelimiter", out var delimiter).Should().BeTrue();
            delimiter.Should().Be("\n"); // TOML parses escape sequences
        }

        [Fact]
        public void HandlesNonExistentFile()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "nonexistent.toml");
            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert - Should not throw, provider should be empty
            provider.TryGet("RabbitMq:Host", out _).Should().BeFalse();
        }

        [Fact]
        public void HandlesInvalidTomlFile()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "invalid.toml");
            var invalidToml = """
                [RabbitMq
                Host = "localhost"
                """; // Missing closing bracket
            File.WriteAllText(configPath, invalidToml);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load(); // Should not throw

            // Assert - Provider should have empty data
            provider.TryGet("RabbitMq:Host", out _).Should().BeFalse();
        }

        [Fact]
        public void HandlesEmptyTomlFile()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "empty.toml");
            File.WriteAllText(configPath, string.Empty);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert - Should not throw
            provider.TryGet("RabbitMq:Host", out _).Should().BeFalse();
        }

        [Fact]
        public void LoadsStringValues()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            var tomlContent = """
                [RabbitMq]
                Host = "rabbitmq.example.com"
                VirtualHost = "/production"
                User = "admin"
                """;
            File.WriteAllText(configPath, tomlContent);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert
            provider.TryGet("RabbitMq:Host", out var host).Should().BeTrue();
            host.Should().Be("rabbitmq.example.com");
            provider.TryGet("RabbitMq:VirtualHost", out var vhost).Should().BeTrue();
            vhost.Should().Be("/production");
            provider.TryGet("RabbitMq:User", out var user).Should().BeTrue();
            user.Should().Be("admin");
        }

        [Fact]
        public void LoadsIntegerValues()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            var tomlContent = """
                [RabbitMq]
                Port = 5672

                [FileConfig]
                MessagesPerFile = 10000
                """;
            File.WriteAllText(configPath, tomlContent);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert
            provider.TryGet("RabbitMq:Port", out var port).Should().BeTrue();
            port.Should().Be("5672");
            provider.TryGet("FileConfig:MessagesPerFile", out var messagesPerFile).Should().BeTrue();
            messagesPerFile.Should().Be("10000");
        }

        [Fact]
        public void LoadsBooleanValues()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            var tomlContent = """
                [Settings]
                Enabled = true
                Debug = false
                """;
            File.WriteAllText(configPath, tomlContent);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert
            provider.TryGet("Settings:Enabled", out var enabled).Should().BeTrue();
            enabled.Should().Be("True");
            provider.TryGet("Settings:Debug", out var debug).Should().BeTrue();
            debug.Should().Be("False");
        }

        [Fact]
        public void HandlesMultipleSections()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            var tomlContent = """
                [RabbitMq]
                Host = "localhost"
                Port = 5672

                [FileConfig]
                MessagesPerFile = 10000

                [CliConfig]
                Verbose = true
                """;
            File.WriteAllText(configPath, tomlContent);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert
            provider.TryGet("RabbitMq:Host", out var host).Should().BeTrue();
            host.Should().Be("localhost");
            provider.TryGet("FileConfig:MessagesPerFile", out var messagesPerFile).Should().BeTrue();
            messagesPerFile.Should().Be("10000");
            provider.TryGet("CliConfig:Verbose", out var verbose).Should().BeTrue();
            verbose.Should().Be("True");
        }

        [Fact]
        public void HandlesCommentsInTomlFile()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            var tomlContent = """
                # This is a comment
                [RabbitMq]
                Host = "localhost" # inline comment
                # Another comment
                Port = 5672
                """;
            File.WriteAllText(configPath, tomlContent);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert
            provider.TryGet("RabbitMq:Host", out var host).Should().BeTrue();
            host.Should().Be("localhost");
            provider.TryGet("RabbitMq:Port", out var port).Should().BeTrue();
            port.Should().Be("5672");
        }

        [Fact]
        public void HandlesDefaultConfigFormat()
        {
            // Arrange
            var configPath = Path.Combine(_tempDir, "config.toml");
            var tomlContent = """
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
            File.WriteAllText(configPath, tomlContent);

            var provider = new TomlConfigurationProvider(configPath);

            // Act
            provider.Load();

            // Assert
            provider.TryGet("RabbitMq:Host", out var host).Should().BeTrue();
            host.Should().Be("localhost");
            provider.TryGet("RabbitMq:Port", out var port).Should().BeTrue();
            port.Should().Be("5672");
            provider.TryGet("RabbitMq:VirtualHost", out var vhost).Should().BeTrue();
            vhost.Should().Be("/");
            provider.TryGet("RabbitMq:User", out var user).Should().BeTrue();
            user.Should().Be("guest");
            provider.TryGet("RabbitMq:Password", out var password).Should().BeTrue();
            password.Should().Be("guest");
            provider.TryGet("RabbitMq:Exchange", out var exchange).Should().BeTrue();
            exchange.Should().Be("amq.direct");
            provider.TryGet("RabbitMq:ClientName", out var clientName).Should().BeTrue();
            clientName.Should().Be("rmq-cli-tool");
            provider.TryGet("FileConfig:MessagesPerFile", out var messagesPerFile).Should().BeTrue();
            messagesPerFile.Should().Be("10000");
        }
    }

    #endregion
}
