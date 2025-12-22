using System.Text.Json;
using RmqCli.E2E.Tests.Infrastructure;
using RmqCli.Shared.Json;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.E2E.Tests.Commands;

/// <summary>
/// E2E tests for the peek command using a real RabbitMQ instance.
/// Covers the critical functionality of non-destructive message inspection and output formats.
/// </summary>
[Collection("RabbitMQ")]
public class PeekCommandTests : IAsyncLifetime
{
    private readonly RabbitMqTestHelpers _helpers;
    private readonly string _tempConfigDir;
    private const string TestQueue = "e2e-peek-test";

    public PeekCommandTests(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _helpers = new RabbitMqTestHelpers(fixture, output);

        // Create a temporary directory for config files to prevent loading user/system config
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"rmq-e2e-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempConfigDir);

        // Override config file paths to prevent loading local user/system config
        Environment.SetEnvironmentVariable("RMQCLI_USER_CONFIG_PATH", Path.Combine(_tempConfigDir, "user-config.toml"));
        Environment.SetEnvironmentVariable("RMQCLI_SYSTEM_CONFIG_PATH", Path.Combine(_tempConfigDir, "system-config.toml"));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test queue after each test
        await _helpers.DeleteQueue(TestQueue);

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
    public async Task Peek_ShouldRetrieveMessages_WhenMessagesAreInQueue()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2", "Message 3" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Verify messages are in queue
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(3);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "3", "--output", "plain"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().Contain("Message 1");
        result.StdoutOutput.Should().Contain("Message 2");
        result.StdoutOutput.Should().Contain("Message 3");

        // Verify messages were NOT removed (peek is non-destructive)
        var finalQueueInfo = await _helpers.GetQueueInfo(TestQueue);
        finalQueueInfo.MessageCount.Should().Be(3);
    }

    [Fact]
    public async Task Peek_ShouldRespectMessageCountLimit_WhenCountSpecified()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2", "Message 3", "Message 4", "Message 5" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act - peek only 3 messages
        var result = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "3", "--output", "plain"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify all 5 messages are still in queue (peek is non-destructive)
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(5);
    }

    [Fact]
    public async Task Peek_ShouldNotRemoveMessages_WhenPeekingMultipleTimes()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act - peek twice
        var result1 = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "2", "--output", "plain"]);
        var result2 = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "2", "--output", "plain"]);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.StdoutOutput.Should().Contain("Message 1");
        result1.StdoutOutput.Should().Contain("Message 2");
        result2.StdoutOutput.Should().Contain("Message 1");
        result2.StdoutOutput.Should().Contain("Message 2");

        // Verify messages are still in queue after peeking twice
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(2);
    }

    [Fact]
    public async Task Peek_ShouldOutputJson_WhenJsonFormatSpecified()
    {
        // Arrange
        var messages = new[] { "Test message" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "1", "--output", "json"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // STDOUT should contain JSON output
        result.StdoutOutput.Should().NotBeEmpty();

        // The message output should conform to the JSON schema for RetrievedMessage
        var jsonMessage = JsonSerializer.Deserialize(result.StdoutOutput, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
        jsonMessage.Should().NotBeNull();

        // The result output should conform to the JSON schema for MessageRetrievalResponse
        var jsonResult = JsonSerializer.Deserialize(result.StderrOutput, JsonSerializationContext.RelaxedEscaping.MessageRetrievalResponse);
        jsonResult.Should().NotBeNull();

        // Verify message is still in queue
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Peek_ShouldOutputTable_WhenTableFormatSpecified()
    {
        // Arrange
        var messages = new[] { "Test message" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act - use no-color to simplify output verification
        var result = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "1", "--output", "table", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StdoutOutput.Should().NotBeNullOrEmpty();
        result.StdoutOutput.Should().Contain("""
                                       ╭─Message #1───────────────────────────────────────────────────────────────────╮
                                       │ Queue             e2e-peek-test                                              │
                                       │ Routing Key       e2e-peek-test                                              │
                                       │ Exchange          -                                                          │
                                       │ Redelivered       No                                                         │
                                       │ ── Properties ────────────────────────────────────────────────────────────── │
                                       │ Message ID        -                                                          │
                                       │ Correlation ID    -                                                          │
                                       │ Timestamp         -                                                          │
                                       │ Content Type      -                                                          │
                                       │ Content Encoding  -                                                          │
                                       │ Delivery Mode     -                                                          │
                                       │ Priority          -                                                          │
                                       │ Expiration        -                                                          │
                                       │ Reply To          -                                                          │
                                       │ Type              -                                                          │
                                       │ App ID            -                                                          │
                                       │ User ID           -                                                          │
                                       │ Cluster ID        -                                                          │
                                       │ ── Body (12 bytes) ───────────────────────────────────────────────────────── │
                                       │ Test message                                                                 │
                                       ╰──────────────────────────────────────────────────────────────────────────────╯
                                       """);

        // Verify message is still in queue
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Peek_ShouldWriteToFile_WhenFileOptionSpecified()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Create temp file path
        var tempFilePath = Path.GetTempFileName();

        // Act - peek with file output
        var result = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "2", "--output", "plain", "--to-file", tempFilePath]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(tempFilePath).Should().BeTrue("output file should be created");
        var fileContent = await File.ReadAllTextAsync(tempFilePath);
        fileContent.Should().Contain("Message 1");
        fileContent.Should().Contain("Message 2");

        // Verify messages are still in queue
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(2);

        // Clean up temp file
        File.Delete(tempFilePath);
    }

    [Fact]
    public async Task Peek_ShouldShowWarning_WhenQueueIsEmpty()
    {
        // Arrange - ensure queue exists but is empty
        await _helpers.DeclareQueue(TestQueue);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "1", "--output", "plain"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Target queue is empty");
        result.StderrOutput.Should().Contain("no messages will be peeked");
    }
}
