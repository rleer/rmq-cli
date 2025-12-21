using RmqCli.E2E.Tests.Infrastructure;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.E2E.Tests.Commands;

/// <summary>
/// E2E tests for command cancellation (CTRL-C) behavior across all commands
/// </summary>
[Collection("RabbitMQ")]
public class CommandCancellationTests : IAsyncLifetime
{
    private readonly RabbitMqTestHelpers _helpers;
    private const string TestQueue = "e2e-cancel-test";

    public CommandCancellationTests(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _helpers = new RabbitMqTestHelpers(fixture, output);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test queue after each test
        await _helpers.DeleteQueue(TestQueue);
    }

    #region Consume Command Cancellation Tests

    [Fact]
    public async Task Consume_WithAckMode_ShouldStopGracefully_WhenCtrlCSignalReceived()
    {
        // Arrange
        var messages = Enumerable.Range(1, 10).Select(i => $"Message {i}").ToArray();
        await _helpers.PublishMessages(TestQueue, messages);

        // Cancel after a very short delay to simulate CTRL-C
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act - Start consuming without count limit
        var result = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--ack-mode", "ack", "--output", "table", "--compact", "--no-color"],
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - The cancellation should work properly with ack mode
        // The process should have stopped gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.StderrOutput.Should().Contain("⚠ Message retrieval cancelled by user");
        result.StderrOutput.Should().NotContain("Retrieved 0 messages");

        // Verify that some messages were consumed before cancellation
        var q = await _helpers.GetQueueInfo(TestQueue);
        q.MessageCount.Should().BeLessThan(10, "some messages should have been consumed before cancellation");
    }

    [Fact]
    public async Task Consume_WithRequeueMode_ShouldStopGracefully_WhenCtrlCSignalReceived()
    {
        // Arrange
        var messages = Enumerable.Range(1, 10).Select(i => $"Message {i}").ToArray();
        await _helpers.PublishMessages(TestQueue, messages);

        // Cancel after a very short delay to simulate CTRL-C
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act - Consume with requeue mode
        var result = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--ack-mode", "requeue", "--output", "plain", "--compact", "--no-color"],
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - The cancellation should work properly with requeue mode
        // The process should have stopped gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.StderrOutput.Should().Contain("⚠ Message retrieval cancelled by user");
        result.StderrOutput.Should().Contain("Retrieved 10 messages");

        // Verify all messages were requeued.
        await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
        var q = await _helpers.GetQueueInfo(TestQueue);
        q.MessageCount.Should().Be(10);
    }

    [Fact]
    public async Task Consume_WithEmptyQueue_ShouldStopGracefully_WhenCtrlCSignalReceived()
    {
        // Arrange - Create an empty queue
        await _helpers.DeclareQueue(TestQueue);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act - Start consuming from empty queue
        var result = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "10", "--ack-mode", "ack", "--output", "plain", "--no-color"],
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - Process should exit gracefully even when canceled before receiving messages
        // The process should have stopped gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.StderrOutput.Should().Contain("⚠ Message retrieval cancelled by user");
        result.StderrOutput.Should().Contain("Retrieved 0 messages");
    }

    #endregion
    
    #region Publish Command Cancellation Tests

    [Fact]
    public async Task Publish_WithBodyFlagAndBurstMode_ShouldStopGracefully_WhenCtrlCSignalReceived()
    {
        // Arrange - Create an empty queue
        await _helpers.DeclareQueue(TestQueue);

        // Cancel after a very short delay to simulate CTRL-C during publishing
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act - Publish from stdin with cancellation
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--no-color", "--body", "Test message", "--burst", "1000"],
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - Process should exit gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.StderrOutput.Should().Contain("⚠ Publishing cancelled by user");

        // Verify that some messages were published before cancellation
        var q = await _helpers.GetQueueInfo(TestQueue);
        q.MessageCount.Should().BeGreaterThan(0, "some messages should have been published before cancellation");
        q.MessageCount.Should().BeLessThan(1000, "not all messages should have been published due to cancellation");
    }

    [Fact]
    public async Task Publish_WithJsonMessageAndBurstMode_ShouldStopGracefully_WhenCtrlCSignalReceived()
    {
        // Arrange - Create an empty queue
        await _helpers.DeclareQueue(TestQueue);

        var jsonMessage = "{\"body\":\"Test message\",\"properties\":{\"contentType\":\"text/plain\"}}";

        // Cancel after a very short delay to simulate CTRL-C during publishing
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act - Publish from stdin with cancellation
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--no-color", "--message", jsonMessage, "--burst", "1000"],
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - Process should exit gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.StderrOutput.Should().Contain("⚠ Publishing cancelled by user");

        // Verify that some messages were published before cancellation
        var q = await _helpers.GetQueueInfo(TestQueue);
        q.MessageCount.Should().BeGreaterThan(0, "some messages should have been published before cancellation");
        q.MessageCount.Should().BeLessThan(1000, "not all messages should have been published due to cancellation");
    }

    [Fact]
    public async Task Publish_WithStdinInput_ShouldStopGracefully_WhenCtrlCSignalReceived()
    {
        // Arrange - Create an empty queue
        await _helpers.DeclareQueue(TestQueue);

        // Prepare multiple messages to send via stdin
        var messages = Enumerable.Range(1, 1000).Select(i => $"Message {i}");
        var stdinInput = string.Join(Environment.NewLine, messages);

        // Cancel after a very short delay to simulate CTRL-C during publishing
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act - Publish from stdin with cancellation
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--no-color"],
            stdinInput: stdinInput,
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - Process should exit gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.StderrOutput.Should().Contain("⚠ Publishing cancelled by user");

        // Verify that some messages were published before cancellation
        var q = await _helpers.GetQueueInfo(TestQueue);
        q.MessageCount.Should().BeGreaterThan(0, "some messages should have been published before cancellation");
        q.MessageCount.Should().BeLessThan(1000, "not all messages should have been published due to cancellation");
    }

    [Theory]
    [InlineData("Plain text message from file")]
    [InlineData("{\"body\":\"JSON message from file\",\"properties\":{\"contentType\":\"application/json\"}}")]
    public async Task Publish_WithFileInput_ShouldStopGracefully_WhenCtrlCSignalReceived(string messageContent)
    {
        // Arrange - Create an empty queue
        await _helpers.DeclareQueue(TestQueue);

        // Create a temp file with many messages
        var tempFilePath = Path.GetTempFileName();
        var messages = Enumerable.Range(1, 1000).Select(_ => messageContent);
        await File.WriteAllTextAsync(tempFilePath, string.Join(Environment.NewLine, messages));

        // Cancel after a very short delay to simulate CTRL-C during publishing
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        try
        {
            // Act - Publish from file with cancellation
            var result = await _helpers.RunRmqCommand(
                ["publish", "--queue", TestQueue, "--message-file", tempFilePath, "--no-color"],
                timeout: TimeSpan.FromSeconds(10), // Fallback timeout
                cancellationToken: cts.Token);

            // Assert - Process should exit gracefully after receiving SIGINT
            result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
            result.StderrOutput.Should().Contain("⚠ Publishing cancelled by user");

            // Verify that some messages were published before cancellation
            var q = await _helpers.GetQueueInfo(TestQueue);
            q.MessageCount.Should().BeGreaterThan(0, "some messages should have been published before cancellation");
            q.MessageCount.Should().BeLessThan(1000, "not all messages should have been published due to cancellation");
        }
        finally
        {
            // Clean up temp file
            File.Delete(tempFilePath);
        }
    }

    #endregion

    #region Peek Command Cancellation Tests

    [Fact]
    public async Task Peek_ShouldStopGracefully_WhenCtrlCSignalReceived()
    {
        // Arrange - publish a few messages
        var messages = Enumerable.Range(1, 500).Select(i => $"Message {i}").ToArray();
        await _helpers.PublishMessages(TestQueue, messages);

        // Cancel after a short delay to simulate CTRL-C while peeking
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var tempFilePath = Path.GetTempFileName();

        // Act
        var result = await _helpers.RunRmqCommand(
            ["peek", "--queue", TestQueue, "--count", "500", "--output", "plain", "--no-color", "--to-file", tempFilePath],
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - Process should exit gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.StderrOutput.Should().Contain("⚠ Message retrieval cancelled by user");
        result.StderrOutput.Should().NotContain("Retrieved 500 messages");

        // Verify messages are still in queue (peek is non-destructive)
        var q = await _helpers.GetQueueInfo(TestQueue);
        q.MessageCount.Should().Be(500, "all messages should still be in queue after peek cancellation");

        // Clean up temp file
        File.Delete(tempFilePath);
    }

    #endregion
}