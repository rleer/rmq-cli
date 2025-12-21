using RmqCli.Subcutaneous.Tests.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.Subcutaneous.Tests.Commands;

/// <summary>
/// Subcutaneous tests for the config command covering detailed scenarios and edge cases.
/// These tests use in-process command execution with a temporary directory for config files.
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
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"rmq-subcut-config-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempConfigDir);
        _tempConfigFilePath = Path.Combine(_tempConfigDir, "config.toml");

        // Set environment variable to use temp directory for user config
        Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", _tempConfigDir);
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

        // Clear environment variable
        Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", null);
    }

    #region config show tests

    [Fact]
    public async Task ConfigShow_WhenNoConfigExists_ShouldReturnWarning()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "show"]);

        // Assert
        result.ExitCode.Should().Be(1);
        result.StdoutOutput.Should().Contain("No configuration file found");
        result.StdoutOutput.Should().Contain("config init");
    }

    [Fact]
    public async Task ConfigShow_WhenConfigExists_ShouldDisplayConfigContent()
    {
        // Arrange
        await _helpers.RunRmqCommand(["config", "init"]);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "show"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain(_tempConfigFilePath);
        result.StdoutOutput.Should().Contain("# rmq Configuration File");
        result.StdoutOutput.Should().Contain("[RabbitMq]");
        result.StdoutOutput.Should().Contain("Host = \"localhost\"");
    }

    [Fact]
    public async Task ConfigShow_WithCustomContent_ShouldDisplayCustomContent()
    {
        // Arrange
        var customContent = "# Custom config\n[RabbitMq]\nHost = \"custom-host\"\nPort = 9999\n";
        await File.WriteAllTextAsync(_tempConfigFilePath, customContent);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "show"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("custom-host");
        result.StdoutOutput.Should().Contain("9999");
    }

    #endregion

    #region config path tests

    [Fact]
    public async Task ConfigPath_WhenNoConfigExists_ShouldReturnWarning()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "path"]);

        // Assert
        result.ExitCode.Should().Be(1);
        result.StdoutOutput.Should().Contain("No configuration file found");
        result.StdoutOutput.Should().Contain("config init");
    }

    [Fact]
    public async Task ConfigPath_WhenConfigExists_ShouldDisplayConfigPath()
    {
        // Arrange
        await _helpers.RunRmqCommand(["config", "init"]);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "path"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("User configuration file path:");
        result.StdoutOutput.Should().Contain(_tempConfigFilePath);
    }

    #endregion

    #region config init tests

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
        configContent.Should().Contain("Port = 5672");
        configContent.Should().Contain("ManagementPort = 15672");
        configContent.Should().Contain("[FileConfig]");
    }

    [Fact]
    public async Task ConfigInit_WhenConfigAlreadyExists_ShouldNotOverwrite()
    {
        // Arrange
        var customContent = "# Custom config\n[RabbitMq]\nHost = \"custom-host\"\n";
        await File.WriteAllTextAsync(_tempConfigFilePath, customContent);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "init"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var configContent = await File.ReadAllTextAsync(_tempConfigFilePath);
        configContent.Should().Be(customContent, "init should not overwrite existing config");
    }

    [Fact]
    public async Task ConfigInit_MultipleCalls_ShouldBeIdempotent()
    {
        // Act
        var result1 = await _helpers.RunRmqCommand(["config", "init"]);
        var result2 = await _helpers.RunRmqCommand(["config", "init"]);
        var result3 = await _helpers.RunRmqCommand(["config", "init"]);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result3.IsSuccess.Should().BeTrue();
        File.Exists(_tempConfigFilePath).Should().BeTrue();
    }

    #endregion

    #region config reset tests

    [Fact]
    public async Task ConfigReset_WhenNoConfigExists_ShouldCreateDefaultConfig()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "reset"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Configuration reset to defaults");
        result.StdoutOutput.Should().Contain(_tempConfigFilePath);
        File.Exists(_tempConfigFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task ConfigReset_WhenConfigExists_ShouldReplaceWithDefaults()
    {
        // Arrange
        var customContent = "# Custom config\n[RabbitMq]\nHost = \"custom-host\"\nPort = 9999\n";
        await File.WriteAllTextAsync(_tempConfigFilePath, customContent);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "reset"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Configuration reset to defaults");

        var configContent = await File.ReadAllTextAsync(_tempConfigFilePath);
        configContent.Should().NotContain("custom-host", "config should be reset to defaults");
        configContent.Should().Contain("# rmq Configuration File");
        configContent.Should().Contain("Host = \"localhost\"");
        configContent.Should().Contain("Port = 5672");
    }

    [Fact]
    public async Task ConfigReset_MultipleTimes_ShouldAlwaysRestoreDefaults()
    {
        // Arrange
        await _helpers.RunRmqCommand(["config", "init"]);

        // Act & Assert - Reset multiple times
        for (var i = 0; i < 3; i++)
        {
            // Modify config
            await File.WriteAllTextAsync(_tempConfigFilePath, $"# Modified {i}\n[RabbitMq]\nHost = \"host-{i}\"\n");

            // Reset
            var result = await _helpers.RunRmqCommand(["config", "reset"]);
            result.IsSuccess.Should().BeTrue();

            // Verify defaults restored
            var content = await File.ReadAllTextAsync(_tempConfigFilePath);
            content.Should().Contain("Host = \"localhost\"");
            content.Should().NotContain($"host-{i}");
        }
    }

    #endregion

    #region config edit tests

    [Fact]
    public async Task ConfigEdit_WhenNoConfigExists_ShouldReturnError()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "edit"]);

        // Assert
        result.ExitCode.Should().Be(1);
        result.StdoutOutput.Should().Contain("Configuration file does not exist");
        result.StdoutOutput.Should().Contain("config init");
    }

    #endregion

    #region workflow tests

    [Fact]
    public async Task ConfigWorkflow_InitShowPathReset_ShouldWorkEndToEnd()
    {
        // 1. Verify no config exists
        var pathResult1 = await _helpers.RunRmqCommand(["config", "path"]);
        pathResult1.ExitCode.Should().Be(1);

        // 2. Initialize config
        var initResult = await _helpers.RunRmqCommand(["config", "init"]);
        initResult.IsSuccess.Should().BeTrue();

        // 3. Show config path
        var pathResult2 = await _helpers.RunRmqCommand(["config", "path"]);
        pathResult2.IsSuccess.Should().BeTrue();
        pathResult2.StdoutOutput.Should().Contain(_tempConfigFilePath);

        // 4. Show config content
        var showResult = await _helpers.RunRmqCommand(["config", "show"]);
        showResult.IsSuccess.Should().BeTrue();
        showResult.StdoutOutput.Should().Contain("Host = \"localhost\"");

        // 5. Modify the config manually
        var customContent = "# Modified\n[RabbitMq]\nHost = \"modified-host\"\n";
        await File.WriteAllTextAsync(_tempConfigFilePath, customContent);

        // 6. Verify modified content shows up
        var showResult2 = await _helpers.RunRmqCommand(["config", "show"]);
        showResult2.StdoutOutput.Should().Contain("modified-host");

        // 7. Reset to defaults
        var resetResult = await _helpers.RunRmqCommand(["config", "reset"]);
        resetResult.IsSuccess.Should().BeTrue();

        // 8. Verify config is back to defaults
        var showResult3 = await _helpers.RunRmqCommand(["config", "show"]);
        showResult3.StdoutOutput.Should().Contain("Host = \"localhost\"");
        showResult3.StdoutOutput.Should().NotContain("modified-host");
    }

    [Fact]
    public async Task ConfigWorkflow_PathShowInit_ShouldGuidUserThroughSetup()
    {
        // 1. Check path when no config exists
        var pathResult1 = await _helpers.RunRmqCommand(["config", "path"]);
        pathResult1.ExitCode.Should().Be(1);
        pathResult1.StdoutOutput.Should().Contain("config init");

        // 2. Try to show config when none exists
        var showResult1 = await _helpers.RunRmqCommand(["config", "show"]);
        showResult1.ExitCode.Should().Be(1);
        showResult1.StdoutOutput.Should().Contain("config init");

        // 3. Initialize config based on guidance
        var initResult = await _helpers.RunRmqCommand(["config", "init"]);
        initResult.IsSuccess.Should().BeTrue();

        // 4. Now path and show should work
        var pathResult2 = await _helpers.RunRmqCommand(["config", "path"]);
        pathResult2.IsSuccess.Should().BeTrue();

        var showResult2 = await _helpers.RunRmqCommand(["config", "show"]);
        showResult2.IsSuccess.Should().BeTrue();
    }

    #endregion
}
