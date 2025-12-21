using RmqCli.E2E.Tests.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.E2E.Tests.Commands;

public class HelpCommandTests
{
    private readonly CliTestHelpers _helpers;

    public HelpCommandTests(ITestOutputHelper output)
    {
        _helpers = new CliTestHelpers(output);
    }
    
    [Fact]
    public async Task Help_ShouldDisplayHelpInformation()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["--help"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Developer focused utility tool for common RabbitMQ tasks");
        result.StdoutOutput.Should().Contain("Usage:");
        result.StdoutOutput.Should().Contain("rmq [command] [options]");
        result.StdoutOutput.Should().Contain("Commands:");
        result.StdoutOutput.Should().Contain("consume");
        result.StdoutOutput.Should().Contain("peek");
        result.StdoutOutput.Should().Contain("publish");
        result.StdoutOutput.Should().Contain("purge");
        result.StdoutOutput.Should().Contain("config");
    } 

    [Fact]
    public async Task Help_ShouldDisplayHelpInformation_ForConfigCommand()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["config", "--help"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Manage configuration files for the rmq CLI tool.");
        result.StdoutOutput.Should().Contain("Usage:");
        result.StdoutOutput.Should().Contain("rmq config [command] [options]");
        result.StdoutOutput.Should().Contain("Commands:");
        result.StdoutOutput.Should().Contain("show");
        result.StdoutOutput.Should().Contain("init");
        result.StdoutOutput.Should().Contain("path");
        result.StdoutOutput.Should().Contain("edit");
        result.StdoutOutput.Should().Contain("reset");
    } 
    
    [Fact]
    public async Task Help_ShouldDisplayHelpInformation_ForPublishCommand()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["publish", "--help"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Publish messages to a queue or via exchange and routing-key.");
        result.StdoutOutput.Should().Contain("Usage:");
        result.StdoutOutput.Should().Contain("rmq publish [options]");
        result.StdoutOutput.Should().Contain("INPUT MODES:");
        result.StdoutOutput.Should().Contain("MESSAGE PROPERTIES:");
        result.StdoutOutput.Should().Contain("JSON MESSAGE FORMAT:");
    } 
    
    [Fact]
    public async Task Help_ShouldDisplayHelpInformation_ForConsumeCommand()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["consume", "--help"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Consume messages from a queue via the AMQP push API");
        result.StdoutOutput.Should().Contain("Usage:");
        result.StdoutOutput.Should().Contain("rmq consume [options]");
        result.StdoutOutput.Should().Contain("OUTPUT:");
        result.StdoutOutput.Should().Contain("EXAMPLES:");
    } 
    
    [Fact]
    public async Task Help_ShouldDisplayHelpInformation_ForPeekCommand()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["peek", "--help"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Peek (inspect) messages from a queue without removing them.");
        result.StdoutOutput.Should().Contain("Usage:");
        result.StdoutOutput.Should().Contain("rmq peek [options]");
        result.StdoutOutput.Should().Contain("WARNING:");
        result.StdoutOutput.Should().Contain("OUTPUT:");
    }
    
    [Fact]
    public async Task Help_ShouldDisplayHelpInformation_ForPurgeCommand()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["purge", "--help"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Purge all ready messages from a queue.");
        result.StdoutOutput.Should().Contain("Usage:");
        result.StdoutOutput.Should().Contain("rmq purge <queue> [options]");
        result.StdoutOutput.Should().Contain("WARNING: This operation is destructive and cannot be undone.");
    }
}
