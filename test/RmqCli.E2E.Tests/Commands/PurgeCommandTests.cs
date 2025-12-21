using System.Text.Json;
using RmqCli.E2E.Tests.Infrastructure;
using RmqCli.Shared.Json;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.E2E.Tests.Commands;

/// <summary>
/// E2E tests for the purge command using a real RabbitMQ instance.
/// Covers the critical functionality of queue purging and output formats.
/// </summary>
[Collection("RabbitMQ")]
public class PurgeCommandTests : IAsyncLifetime
{
    private readonly RabbitMqTestHelpers _helpers;
    private const string TestQueue = "e2e-purge-test";

    public PurgeCommandTests(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _helpers = new RabbitMqTestHelpers(fixture, output);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test queue after each test
        await _helpers.DeleteQueue(TestQueue);
    }

    [Fact]
    public async Task Purge_ShouldRemoveAllMessages_WhenQueueHasMessages()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2", "Message 3", "Message 4", "Message 5" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Verify messages are in queue
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(5);

        // Act - use --force to skip confirmation prompt
        var result = await _helpers.RunRmqCommand(
            ["purge", TestQueue, "--force", "--no-color", "--output", "plain"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Purged 5 messages");
        result.StderrOutput.Should().Contain(TestQueue);

        // Verify queue is empty after purge
        var finalQueueInfo = await _helpers.GetQueueInfo(TestQueue);
        finalQueueInfo.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task Purge_ShouldSucceed_WhenQueueIsEmpty()
    {
        // Arrange - create empty queue
        await _helpers.DeclareQueue(TestQueue);

        // Verify queue is empty
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(0);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["purge", TestQueue, "--force", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Purged messages");
        result.StderrOutput.Should().Contain(TestQueue);
    }

    [Fact]
    public async Task Purge_ShouldOutputJson_WhenJsonFormatSpecified()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["purge", TestQueue, "--force", "--output", "json"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // STDERR should contain JSON output
        result.StderrOutput.Should().NotBeEmpty();

        // The result output should conform to the JSON schema for PurgeResponse
        var jsonResult = JsonSerializer.Deserialize(result.StderrOutput, JsonSerializationContext.RelaxedEscaping.PurgeResponse);
        jsonResult.Should().NotBeNull();
        jsonResult.Queue.Should().Be(TestQueue);
        jsonResult.Status.Should().Be("success");
        jsonResult.PurgedMessages.Should().Be(2);

        // Verify queue is empty
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task Purge_WithUseApi_ShouldRemoveAllMessages()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2", "Message 3" };
        await _helpers.PublishMessages(TestQueue, messages);

        // Verify messages are in queue
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(3);

        // Act - use --use-api option
        var result = await _helpers.RunRmqCommand(
            ["purge", TestQueue, "--force", "--use-api", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Purged 3 messages");
        result.StderrOutput.Should().Contain(TestQueue);

        // Verify queue is empty after purge
        var finalQueueInfo = await _helpers.GetQueueInfo(TestQueue);
        finalQueueInfo.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task Purge_ShouldFail_WhenQueueDoesNotExist()
    {
        // Arrange - use a non-existent queue name
        var nonExistentQueue = "non-existent-queue-" + Guid.NewGuid();

        // Act
        var result = await _helpers.RunRmqCommand(
            ["purge", nonExistentQueue, "--force", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("Failed to purge queue");
        result.StderrOutput.Should().Contain(nonExistentQueue);
    }
}
