using System.Text.Json;
using RmqCli.E2E.Tests.Infrastructure;
using RmqCli.Shared.Json;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.E2E.Tests.Commands;

/// <summary>
/// E2E tests for the consume command using a real RabbitMQ instance.
/// These tests verify the full workflow from publishing messages to consuming them via the CLI.
/// </summary>
[Collection("RabbitMQ")]
public class ConsumeCommandTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly RabbitMqTestHelpers _helpers;
    private const string TestQueue = "e2e-consume-test";

    public ConsumeCommandTests(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _output = output;
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

        // The message output should be parseable as JSON
        var parsing = () => JsonDocument.Parse(result.Output);
        parsing.Should().NotThrow("message output should be valid JSON");

        // {"body":"Test message","properties":{"appId":null,"clusterId":null,"contentType":null,"contentEncoding":null,"correlationId":null,"deliveryMode":null,"expiration":null,"messageId":null,"priority":null,"replyTo":null,"timestamp":null,"type":null,"userId":null},"headers":null,"exchange":"","routingKey":"e2e-consume-test","queue":"e2e-consume-test","deliveryTag":1,"redelivered":false,"bodySizeBytes":12,"bodySize":"12 bytes"}    
        var jsonMessage = JsonSerializer.Deserialize(result.Output, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
        jsonMessage.Should().NotBeNull();
        jsonMessage.Queue.Should().Be(TestQueue);
        jsonMessage.RoutingKey.Should().Be(TestQueue);
        jsonMessage.Redelivered.Should().Be(false);
        jsonMessage.Headers.Should().BeNull();
        jsonMessage.Body.Should().Be("Test message");
        jsonMessage.BodySize.Should().Be("12 bytes");
        jsonMessage.BodySizeBytes.Should().Be(12);
        jsonMessage.DeliveryTag.Should().Be(1);

        // The result output should be parseable as JSON
        parsing = () => JsonDocument.Parse(result.ErrorOutput);
        parsing.Should().NotThrow("result output should be valid JSON");

        // {"result":{"messages_received":1,"messages_processed":1,"messages_skipped":0,"duration_ms":14.556,"duration":"14ms","ack_mode":"Ack","retrieval_mode":"subscribe","messages_per_second":68.7,"total_size_bytes":12,"total_size":"12 bytes"},"queue":"e2e-consume-test","status":"success","timestamp":"2025-11-16T17:04:56.984718Z","error":null}
        var jsonResult = JsonSerializer.Deserialize(result.ErrorOutput, JsonSerializationContext.RelaxedEscaping.MessageRetrievalResponse);
        jsonResult.Should().NotBeNull();
        jsonResult.Queue.Should().Be(TestQueue);
        jsonResult.Status.Should().Be("success");
        jsonResult.Result.Should().NotBeNull();
        jsonResult.Timestamp.Should().BeBefore(DateTime.UtcNow);
        jsonResult.Result.MessagesReceived.Should().Be(1);
        jsonResult.Result.MessagesProcessed.Should().Be(1);
        jsonResult.Result.MessagesSkipped.Should().Be(0);
        jsonResult.Result.DurationMs.Should().BeGreaterThan(0);
        jsonResult.Result.Duration.Should().NotBeEmpty();
        jsonResult.Result.AckMode.Should().Be("Ack");
        jsonResult.Result.RetrievalMode.Should().Be("subscribe");
        jsonResult.Result.MessagesPerSecond.Should().NotBe(0);
        jsonResult.Result.TotalSizeBytes.Should().Be(12);
        jsonResult.Result.TotalSize.Should().Be("12 bytes");
    }
}