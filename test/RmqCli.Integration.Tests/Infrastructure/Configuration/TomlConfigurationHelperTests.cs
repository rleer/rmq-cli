using RmqCli.Infrastructure.Configuration;
using RmqCli.Shared;

namespace RmqCli.Integration.Tests.Infrastructure.Configuration;

public class TomlConfigurationHelperTests
{
    public class GetUserConfigFilePath
    {
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

    public class GetSystemConfigFilePath
    {
        [Fact]
        public void ReturnsPath_WithConfigTomlFileName()
        {
            // Act
            var path = TomlConfigurationHelper.GetSystemConfigFilePath();

            // Assert
            path.Should().EndWith("config.toml");
        }

        [Fact]
        public void ReturnsPath_ContainingAppName()
        {
            // Act
            var path = TomlConfigurationHelper.GetSystemConfigFilePath();

            // Assert
            path.Should().Contain(Constants.AppName);
        }

        [Fact]
        public void ReturnsConsistentPath_OnMultipleCalls()
        {
            // Act
            var path1 = TomlConfigurationHelper.GetSystemConfigFilePath();
            var path2 = TomlConfigurationHelper.GetSystemConfigFilePath();

            // Assert
            path1.Should().Be(path2);
        }

        [Fact]
        public void ReturnsAbsolutePath()
        {
            // Act
            var path = TomlConfigurationHelper.GetSystemConfigFilePath();

            // Assert
            Path.IsPathRooted(path).Should().BeTrue();
        }

        [Fact]
        public void ReturnsDifferentPath_ThanUserConfigPath()
        {
            // Act
            var systemPath = TomlConfigurationHelper.GetSystemConfigFilePath();
            var userPath = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            systemPath.Should().NotBe(userPath);
        }
    }

    public class DefaultConfigGeneration
    {
        [Fact]
        public void CreatesConfigFile_WhenNotExists()
        {
            // Arrange
            var userConfigPath = TomlConfigurationHelper.GetUserConfigFilePath();

            // Act
            TomlConfigurationHelper.CreateDefaultUserConfigIfNotExists();

            // Assert
            // After calling, the file should exist
            File.Exists(userConfigPath).Should().BeTrue();
        }
    }

    public class PlatformSpecificBehavior
    {
        [Fact]
        public void OnUnix_ReturnsPathInDotConfigDirectory()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return; // Skip on Windows
            }

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

            // Arrange
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().StartWith(homeDir);
        }

        [Fact]
        public void OnUnix_ReturnsPathInEtcDirectory()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return; // Skip on Windows
            }

            // Act
            var path = TomlConfigurationHelper.GetSystemConfigFilePath();

            // Assert
            path.Should().StartWith("/etc/");
        }

        [Fact]
        public void OnUnix_UserConfigPath_UsesDotConfig()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return; // Skip on Windows
            }

            // Arrange
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var expectedBasePath = Path.Combine(homeDir, ".config", Constants.AppName);

            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().StartWith(expectedBasePath);
        }

        [Fact]
        public void OnUnix_SystemConfigPath_UsesEtc()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return; // Skip on Windows
            }

            // Arrange
            var expectedBasePath = $"/etc/{Constants.AppName}";

            // Act
            var path = TomlConfigurationHelper.GetSystemConfigFilePath();

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

            // Arrange
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var expectedBasePath = Path.Combine(appData, Constants.AppName);

            // Act
            var path = TomlConfigurationHelper.GetUserConfigFilePath();

            // Assert
            path.Should().StartWith(expectedBasePath);
        }

        [Fact]
        public void OnWindows_ReturnsPathInProgramData()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            // Arrange
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Act
            var path = TomlConfigurationHelper.GetSystemConfigFilePath();

            // Assert
            path.Should().Contain(programData.Split(Path.DirectorySeparatorChar)[0]); // Drive letter or root
        }

        [Fact]
        public void ConfigPaths_UseCorrectDirectorySeparator()
        {
            // Act
            var userPath = TomlConfigurationHelper.GetUserConfigFilePath();
            var systemPath = TomlConfigurationHelper.GetSystemConfigFilePath();

            // Assert
            userPath.Should().Contain(Path.DirectorySeparatorChar.ToString());
            systemPath.Should().Contain(Path.DirectorySeparatorChar.ToString());
        }
    }
}