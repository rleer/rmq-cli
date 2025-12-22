using RmqCli.Infrastructure.Configuration;
using RmqCli.Shared;

namespace RmqCli.Integration.Tests.Infrastructure.Configuration;

public class TomlConfigurationHelperTests
{
    [Collection("Configuration Helpers Tests")]
    public class GetUserConfigFilePath : IDisposable
    {
        public GetUserConfigFilePath()
        {
            // Ensure no env var override is active from previous tests
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
        }

        [Fact]
        public void ReturnsPath_WithConfigTomlFileName()
        {
            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().EndWith("config.toml");
        }

        [Fact]
        public void ReturnsPath_ContainingAppName()
        {
            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().Contain(Constants.AppName);
        }

        [Fact]
        public void ReturnsConsistentPath_OnMultipleCalls()
        {
            // Act
            var path1 = TomlConfigurationHelper.GetUserConfigFilePath();
            var path2 = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path1.Should().Be(path2);
        }

        [Fact]
        public void ReturnsAbsolutePath()
        {
            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            Path.IsPathRooted(path).Should().BeTrue();
        }
    }

    [Collection("Configuration Helpers Tests")]
    public class EnvironmentVariableOverrides : IDisposable
    {
        public EnvironmentVariableOverrides()
        {
            // Ensure no env var override is active from previous tests
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
        }

        [Fact]
        public void GetUserConfigFilePath_RespectsEnvVar()
        {
            // Arrange
            var expectedPath = Path.Combine(Path.GetTempPath(), "custom", "user", "config.toml");
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", expectedPath);

            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().Be(expectedPath);
        }
    }

    [Collection("Configuration Helpers Tests")]
    public class DefaultConfigGeneration : IDisposable
    {
        private readonly string _tempDir;

        public DefaultConfigGeneration()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"rmq-helper-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", Path.Combine(_tempDir, "config.toml"));
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public void CreatesConfigFile_WhenNotExists()
        {
            // Arrange
            var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath();
            if (File.Exists(userConfigPath)) File.Delete(userConfigPath);

            // Act
            TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();

            // Assert
            File.Exists(userConfigPath).Should().BeTrue();
            var content = File.ReadAllText(userConfigPath);
            content.Should().Contain("[RabbitMq]");
        }

        [Fact]
        public void DoesNotOverwrite_WhenExists()
        {
            // Arrange
            var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath();
            File.WriteAllText(userConfigPath, "existing content");

            // Act
            TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();

            // Assert
            File.ReadAllText(userConfigPath).Should().Be("existing content");
        }

        [Fact]
        public void CreatesConfigDirectory_WhenNotExists()
        {
            // Arrange
            var nonExistingDir = Path.Combine(Path.GetTempPath(), $"rmq-helper-tests-{Guid.NewGuid()}");
            var configFilePath = Path.Combine(nonExistingDir, "config.toml");
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", configFilePath);

            // Act
            TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();

            // Assert
            Directory.Exists(nonExistingDir).Should().BeTrue();

            // Cleanup
            if (Directory.Exists(nonExistingDir))
            {
                Directory.Delete(nonExistingDir, recursive: true);
            }
        }
    }

    [Collection("Configuration Helpers Tests")]
    public class PlatformSpecificBehavior : IDisposable
    {
        public PlatformSpecificBehavior()
        {
            // Ensure no env var override is active from previous tests
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
        }

        [Fact]
        public void OnUnix_ReturnsPathInDotConfigDirectory()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return; // Skip on Windows
            }

            // Ensure no env var override is active
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);

            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().Contain(".config");
        }

        [Fact]
        public void OnUnix_ReturnsPathUnderHomeDirectory()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return; // Skip on Windows
            }

            // Ensure no env var override is active
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);

            // Arrange
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().StartWith(homeDir);
        }

        [Fact]
        public void OnUnix_UserConfigPath_UsesDotConfig()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return; // Skip on Windows
            }

            // Ensure no env var override is active
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);

            // Arrange
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var expectedBasePath = Path.Combine(homeDir, ".config", Constants.AppName);

            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().StartWith(expectedBasePath);
        }

        [Fact]
        public void OnWindows_UserConfigPath_UsesAppData()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            // Ensure no env var override is active
            Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);

            // Arrange
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var expectedBasePath = Path.Combine(appData, Constants.AppName);

            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().StartWith(expectedBasePath);
        }

        [Fact]
        public void UserConfigPath_UsesCorrectDirectorySeparator()
        {
            // Act
            var userPath = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            userPath.Should().Contain(Path.DirectorySeparatorChar.ToString());
        }
    }
}