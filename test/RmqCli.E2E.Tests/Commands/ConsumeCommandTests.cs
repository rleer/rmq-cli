using System.Text.Json;
using RmqCli.E2E.Tests.Infrastructure;
using RmqCli.Shared.Json;
using RmqCli.Tests.Shared.Infrastructure;

namespace RmqCli.E2E.Tests.Commands;

/// <summary>
/// E2E tests for the consume command using a real RabbitMQ instance.
/// Covers the critical functionality of message consumption, acknowledgment modes, and output formats.
/// </summary>
[Collection("RabbitMQ")]
public class ConsumeCommandTests : IAsyncLifetime
{
    private readonly RabbitMqTestHelpers _helpers;
    private const string TestQueue = "e2e-consume-test";

    public ConsumeCommandTests(RabbitMqFixture fixture)
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
    public async Task Consume_ShouldRetrieveMessages_WhenMessagesAreInQueue()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2", "Message 3" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Verify messages are in queue
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(3);

        // Act
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --count 3 --ack-mode ack --output plain");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("Message 1");
        result.Output.Should().Contain("Message 2");
        result.Output.Should().Contain("Message 3");

        // Verify messages were consumed (queue should be empty)
        var finalQueueInfo = await _helpers.GetQueueInfo(TestQueue);
        finalQueueInfo.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task Consume_ShouldRespectMessageCountLimit_WhenCountSpecified()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2", "Message 3", "Message 4", "Message 5" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act - consume only 3 messages
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --count 3 --ack-mode ack --output plain");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify only 3 messages were consumed (2 should remain)
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(2);
    }

    [Fact]
    public async Task Consume_ShouldRequeueMessages_WhenRequeueAckModeUsed()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act - consume with requeue mode
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --count 2 --ack-mode requeue --output plain");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("Message 1");
        result.Output.Should().Contain("Message 2");

        // Verify messages were requeued (still in queue)
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(2);
    }

    [Fact]
    public async Task Consume_ShouldRejectMessages_WhenRejectAckModeUsed()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act - consume with reject mode (no requeue)
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --count 2 --ack-mode reject --output plain");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify messages were rejected and not requeued (queue should be empty)
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task Consume_ShouldOutputJson_WhenJsonFormatSpecified()
    {
        // Arrange
        var messages = new[] { "Test message" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --count 1 --ack-mode ack --output json");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // STDOUT should contain JSON output
        result.Output.Should().NotBeEmpty();

        // The message output should conform to the JSON schema for RetrievedMessage
        var jsonMessage = JsonSerializer.Deserialize(result.Output, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
        jsonMessage.Should().NotBeNull();

        // The result output should conform to the JSON schema for MessageRetrievalResponse
        var jsonResult = JsonSerializer.Deserialize(result.ErrorOutput, JsonSerializationContext.RelaxedEscaping.MessageRetrievalResponse);
        jsonResult.Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_ShouldOutputTable_WhenTableFormatSpecified()
    {
        // Arrange
        var messages = new[] { "Test message" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act - use no-color to simplify output verification
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --count 1 --ack-mode ack --output table --no-color");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().NotBeNullOrEmpty();
        result.Output.Should().Contain("""
                                       ╭─Message #1───────────────────────────────────────────────────────────────────╮
                                       │ Queue             e2e-consume-test                                           │
                                       │ Routing Key       e2e-consume-test                                           │
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
    }

    [Fact]
    public async Task Consume_ShouldWriteToFile_WhenFileOptionSpecified()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2" };
        await _helpers.PublishMessages(TestQueue, messages);
        
        // Create temp file path
        var tempFilePath = Path.GetTempFileName();

        // Act - consume with reject mode (no requeue)
        var result = await _helpers.RunRmqCommand(
            $"consume --queue {TestQueue} --count 2 --ack-mode reject --output plain --to-file {tempFilePath}");

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(tempFilePath).Should().BeTrue("output file should be created");
        var fileContent = await File.ReadAllTextAsync(tempFilePath);
        fileContent.Should().Contain("Message 1");
        fileContent.Should().Contain("Message 2");
        
        // Clean up temp file
        File.Delete(tempFilePath);
    }
}