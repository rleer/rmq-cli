using RmqCli.E2E.Tests.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.E2E.Tests.Commands;

/// <summary>
/// E2E tests for the config command covering the happy path.
/// These tests verify the critical functionality using the published executable.
/// For detailed edge cases and error scenarios, see RmqCli.Subcutaneous.Tests.
/// </summary>
public class ConfigCommandTests : IDisposable
{
    private readonly CliTestHelpers _helpers;
    private readonly string _tempConfigDir;
    private readonly string _tempConfigFilePath;

    public ConfigCommandTests(ITestOutputHelper output)
    {
        _helpers = new CliTestHelpers(output);

        // Create a temporary directory for config files
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"rmq-e2e-config-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempConfigDir);
        _tempConfigFilePath = Path.Combine(_tempConfigDir, "config.toml");

        // Override config file paths to prevent loading local user/system config
        Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", _tempConfigFilePath);
        Environment.SetEnvironmentVariable("RMQCLI_SYSTEM_CONFIG_PATH", Path.Combine(_tempConfigDir, "system-config.toml"));
    }

    public void Dispose()
    {
        // Clean up temp directory
        if (Directory.Exists(_tempConfigDir))
        {
            try
            {
                Directory.Delete(_tempConfigDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clear environment variables
        Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
        Environment.SetEnvironmentVariable("RMQCLI_SYSTEM_CONFIG_PATH", null);
    }

    [Fact]
    public async Task ConfigInit_ShouldCreateDefaultConfigFile()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "init"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(_tempConfigFilePath).Should().BeTrue();

        var configContent = await File.ReadAllTextAsync(_tempConfigFilePath);
        configContent.Should().Contain("# rmq Configuration File");
        configContent.Should().Contain("[RabbitMq]");
        configContent.Should().Contain("Host = \"localhost\"");
    }

    [Fact]
    public async Task ConfigShow_ShouldDisplayConfigContent()
    {
        // Arrange
        await _helpers.RunRmqCommand(["config", "init"]);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "show"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain(_tempConfigFilePath);
        result.StdoutOutput.Should().Contain("[RabbitMq]");
        result.StdoutOutput.Should().Contain("Host = \"localhost\"");
    }

    [Fact]
    public async Task ConfigPath_ShouldDisplayConfigPath()
    {
        // Arrange
        await _helpers.RunRmqCommand(["config", "init"]);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "path"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("User configuration file path:");
        result.StdoutOutput.Should().Contain(_tempConfigFilePath);
        
        result.StderrOutput.Should().BeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ConfigReset_ShouldReplaceWithDefaults()
    {
        // Arrange
        var customContent = "# Custom config\n[RabbitMq]\nHost = \"custom-host\"\n";
        await File.WriteAllTextAsync(_tempConfigFilePath, customContent);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "reset"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Configuration reset to defaults");

        var configContent = await File.ReadAllTextAsync(_tempConfigFilePath);
        configContent.Should().Contain("Host = \"localhost\"");
        configContent.Should().NotContain("custom-host");
        
        result.StdoutOutput.Should().BeNullOrWhiteSpace();
    }
}
