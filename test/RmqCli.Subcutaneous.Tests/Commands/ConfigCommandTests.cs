using RmqCli.Shared;
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
    }

    #region config show tests

    [Fact]
    public async Task ConfigShow_WhenNoConfigExists_ShouldReturnWarning()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "show", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.ExitCode.Should().Be(1);
        result.StderrOutput.Should().Contain("No configuration file found");
        result.StderrOutput.Should().Contain("config init");
    }

    [Fact]
    public async Task ConfigShow_WhenConfigExists_ShouldDisplayConfigContent()
    {
        // Arrange
        await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "show", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain($"Current user configuration file: {_tempConfigFilePath}");
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
        var result = await _helpers.RunRmqCommand(["config", "show", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("custom-host");
        result.StdoutOutput.Should().Contain("9999");
    }

    [Fact]
    public async Task ConfigShow_WithQuietFlag_ShouldHidePathMessage()
    {
        // Arrange
        await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "show", "--quiet", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().BeEmpty();
        result.StdoutOutput.Should().Contain("Host = \"localhost\"");
    }

    #endregion

    #region config path tests

    [Fact]
    public async Task ConfigPath_WhenNoConfigExists_ShouldReturnWarning()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "path", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.ExitCode.Should().Be(1);
        result.StderrOutput.Should().Contain("No configuration file found");
        result.StderrOutput.Should().Contain("config init");
    }

    [Fact]
    public async Task ConfigPath_WhenConfigExists_ShouldDisplayConfigPath()
    {
        // Arrange
        await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "path", "--user-config-path", _tempConfigFilePath]);

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
        var result = await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain($"{Constants.SuccessSymbol} Created default configuration file at:");
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
    public async Task ConfigInit_WithQuietFlag_ShouldHideSuccessMessage()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "init", "--quiet", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().BeEmpty();
        File.Exists(_tempConfigFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task ConfigInit_WhenConfigAlreadyExists_ShouldNotOverwrite()
    {
        // Arrange
        var customContent = "# Custom config\n[RabbitMq]\nHost = \"custom-host\"\n";
        await File.WriteAllTextAsync(_tempConfigFilePath, customContent);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain($"{Constants.WarningSymbol} Config already exists at");
        var configContent = await File.ReadAllTextAsync(_tempConfigFilePath);
        configContent.Should().Be(customContent, "init should not overwrite existing config");
    }

    [Fact]
    public async Task ConfigInit_MultipleCalls_ShouldInitOnlyOnce()
    {
        // Act
        var result1 = await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);
        var result2 = await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);
        var result3 = await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        // Since init returns error if config exists, result2 and 3 should fail
        result2.IsSuccess.Should().BeFalse();
        result3.IsSuccess.Should().BeFalse();
        File.Exists(_tempConfigFilePath).Should().BeTrue();
    }

    #endregion

    #region config reset tests

    [Fact]
    public async Task ConfigReset_WhenNoConfigExists_ShouldCreateDefaultConfig()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "reset", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Configuration reset to defaults");
        result.StderrOutput.Should().Contain(_tempConfigFilePath);
        File.Exists(_tempConfigFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task ConfigReset_WithQuietFlag_ShouldHideSuccessMessage()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "reset", "--quiet", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().BeEmpty();
        File.Exists(_tempConfigFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task ConfigReset_WhenConfigExists_ShouldReplaceWithDefaults()
    {
        // Arrange
        var customContent = "# Custom config\n[RabbitMq]\nHost = \"custom-host\"\nPort = 9999\n";
        await File.WriteAllTextAsync(_tempConfigFilePath, customContent);

        // Act
        var result = await _helpers.RunRmqCommand(["config", "reset", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Configuration reset to defaults");

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
        await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);

        // Act & Assert - Reset multiple times
        for (var i = 0; i < 3; i++)
        {
            // Modify config
            await File.WriteAllTextAsync(_tempConfigFilePath, $"# Modified {i}\n[RabbitMq]\nHost = \"host-{i}\"\n");

            // Reset
            var result = await _helpers.RunRmqCommand(["config", "reset", "--user-config-path", _tempConfigFilePath]);
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
        var result = await _helpers.RunRmqCommand(["config", "edit", "--user-config-path", _tempConfigFilePath]);

        // Assert
        result.ExitCode.Should().Be(1);
        result.StderrOutput.Should().Contain("No configuration file found");
        result.StderrOutput.Should().Contain("'config init'");
    }

    #endregion

    #region workflow tests

    [Fact]
    public async Task ConfigWorkflow_InitShowPathReset_ShouldWorkEndToEnd()
    {
        // 1. Verify no config exists
        var pathResult1 = await _helpers.RunRmqCommand(["config", "path", "--user-config-path", _tempConfigFilePath]);
        pathResult1.ExitCode.Should().Be(1);

        // 2. Initialize config
        var initResult = await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);
        initResult.IsSuccess.Should().BeTrue();

        // 3. Show config path
        var pathResult2 = await _helpers.RunRmqCommand(["config", "path", "--user-config-path", _tempConfigFilePath]);
        pathResult2.IsSuccess.Should().BeTrue();
        pathResult2.StdoutOutput.Should().Contain(_tempConfigFilePath);

        // 4. Show config content
        var showResult = await _helpers.RunRmqCommand(["config", "show", "--user-config-path", _tempConfigFilePath]);
        showResult.IsSuccess.Should().BeTrue();
        showResult.StdoutOutput.Should().Contain("Host = \"localhost\"");

        // 5. Modify the config manually
        var customContent = "# Modified\n[RabbitMq]\nHost = \"modified-host\"\n";
        await File.WriteAllTextAsync(_tempConfigFilePath, customContent);

        // 6. Verify modified content shows up
        var showResult2 = await _helpers.RunRmqCommand(["config", "show", "--user-config-path", _tempConfigFilePath]);
        showResult2.StdoutOutput.Should().Contain("modified-host");

        // 7. Reset to defaults
        var resetResult = await _helpers.RunRmqCommand(["config", "reset", "--user-config-path", _tempConfigFilePath]);
        resetResult.IsSuccess.Should().BeTrue();

        // 8. Verify config is back to defaults
        var showResult3 = await _helpers.RunRmqCommand(["config", "show", "--user-config-path", _tempConfigFilePath]);
        showResult3.StdoutOutput.Should().Contain("Host = \"localhost\"");
        showResult3.StdoutOutput.Should().NotContain("modified-host");
    }

    [Fact]
    public async Task ConfigWorkflow_PathShowInit_ShouldGuidUserThroughSetup()
    {
        // 1. Check path when no config exists
        var pathResult1 = await _helpers.RunRmqCommand(["config", "path", "--user-config-path", _tempConfigFilePath]);
        pathResult1.ExitCode.Should().Be(1);
        pathResult1.StderrOutput.Should().Contain("Run the 'config init' command");

        // 2. Try to show config when none exists
        var showResult1 = await _helpers.RunRmqCommand(["config", "show", "--user-config-path", _tempConfigFilePath]);
        showResult1.ExitCode.Should().Be(1);
        showResult1.StderrOutput.Should().Contain("Run the 'config init' command");

        // 3. Initialize config based on guidance
        var initResult = await _helpers.RunRmqCommand(["config", "init", "--user-config-path", _tempConfigFilePath]);
        initResult.IsSuccess.Should().BeTrue();

        // 4. Now path and show should work
        var pathResult2 = await _helpers.RunRmqCommand(["config", "path", "--user-config-path", _tempConfigFilePath]);
        pathResult2.IsSuccess.Should().BeTrue();
        pathResult2.StdoutOutput.Should().Contain(_tempConfigFilePath);

        var showResult2 = await _helpers.RunRmqCommand(["config", "show", "--user-config-path", _tempConfigFilePath]);
        showResult2.IsSuccess.Should().BeTrue();
        showResult2.StderrOutput.Should().Contain($"Current user configuration file: {_tempConfigFilePath}");
        showResult2.StdoutOutput.Should().Contain("Host = \"localhost\"");
    }

    #endregion
}