using RmqCli.E2E.Tests.Infrastructure;
using RmqCli.Tests.Shared.Infrastructure;

namespace RmqCli.E2E.Tests.Commands;

/// <summary>
/// E2E tests for consume command cancellation (CTRL-C) behavior
/// </summary>
[Collection("RabbitMQ")]
public class ConsumeCancellationTests : IAsyncLifetime
{
    private readonly RabbitMqTestHelpers _helpers;
    private const string TestQueue = "e2e-cancel-test";

    public ConsumeCancellationTests(RabbitMqFixture fixture)
    {
        _helpers = new RabbitMqTestHelpers(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test queue after each test
        await _helpers.DeleteQueue(TestQueue);
    }

    [Fact]
    public async Task Consume_WithAckMode_ShouldStopGracefully_WhenCtrlCSignalReceived()
    {
        // Arrange
        var messages = Enumerable.Range(1, 10).Select(i => $"Message {i}").ToArray();
        await _helpers.PublishMessages(TestQueue, messages);

        // Cancel after a very short delay to simulate CTRL-C
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act - Start consuming without count limit
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --ack-mode ack --output table --no-color",
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - The cancellation should work properly with ack mode
        // The process should have stopped gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.ErrorOutput.Should().Contain("⚠ Message retrieval cancelled by user");
        result.ErrorOutput.Should().NotContain("Retrieved 0 messages");
        
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act - Consume with requeue mode
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --ack-mode requeue --output plain --no-color",
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - The cancellation should work properly with requeue mode
        // The process should have stopped gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.ErrorOutput.Should().Contain("⚠ Message retrieval cancelled by user");
        result.ErrorOutput.Should().Contain("Retrieved 10 messages");
        
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
            $"consume --queue {TestQueue} --count 10 --ack-mode ack --output plain --no-color",
            timeout: TimeSpan.FromSeconds(10), // Fallback timeout
            cancellationToken: cts.Token);

        // Assert - Process should exit gracefully even when canceled before receiving messages
        // The process should have stopped gracefully after receiving SIGINT
        result.ExitCode.Should().Be(0, "process should exit gracefully after receiving SIGINT");
        result.ErrorOutput.Should().Contain("⚠ Message retrieval cancelled by user");
        
        result.ErrorOutput.Should().Contain("Retrieved 0 messages");
    }
}
