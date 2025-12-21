using RmqCli.E2E.Tests.Infrastructure;
using RmqCli.Shared.Json;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.E2E.Tests.Commands;

/// <summary>
/// E2E tests for the publish command using a real RabbitMQ instance.
/// Covers the critical functionality of message publishing.
/// </summary>
[Collection("RabbitMQ")]
public class PublishCommandTests : IAsyncLifetime
{
    private readonly RabbitMqTestHelpers _helpers;
    private const string TestQueue = "e2e-publish-test";

    public PublishCommandTests(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _helpers = new RabbitMqTestHelpers(fixture, output);
    }

    public async Task InitializeAsync()
    {
        // Ensure test queue exists before each test
        await _helpers.DeclareQueue(TestQueue);
    }

    public async Task DisposeAsync()
    {
        // Clean up test queue after each test
        try
        {
            await _helpers.DeleteQueue(TestQueue);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task Publish_WithBodyFlag_ShouldPublishMessage()
    {
        // Arrange
        var messageBody = "Test message from E2E test";

        // Act
        var result = await _helpers.RunRmqCommand(["publish", "--queue", TestQueue, "--body", messageBody, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithMessageFlag_ShouldPublishMessage()
    {
        // Arrange
        var message = "{\"body\":\"Message using --message flag\", \"properties\":{\"priority\":5}}";

        // Act
        var result = await _helpers.RunRmqCommand(["publish", "--queue", TestQueue, "--message", message, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Plain text message from file")]
    [InlineData("{\"body\":\"JSON message from file\",\"properties\":{\"contentType\":\"application/json\"}}")]
    public async Task Publish_WithMessageFileFlag_ShouldPublishMessage(string messageContent)
    {
        // Arrange - write plain text message to temp file
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, messageContent);

        // Act
        var result = await _helpers.RunRmqCommand(["publish", "--queue", TestQueue, "--message-file", tempFilePath, "--no-color"]);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");
       
        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1); 
        
        // Clean up temp file
        File.Delete(tempFilePath);
    }

    [Fact]
    public async Task Publish_WithStdinInput_ShouldPublishMessage()
    {
        // Arrange
        var messageBody = "Message from stdin";

        // Act - pipe message via stdin
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--no-color"],
            stdinInput: messageBody);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithExchangeAndRoutingKey_ShouldPublishMessage()
    {
        // Arrange
        await _helpers.DeclareExchange("e2e.test.exchange", "direct");
        await _helpers.DeclareBinding("e2e.test.exchange", TestQueue, "e2e.test.key");
        
        // Act
        var result = await _helpers.RunRmqCommand(["publish", "--exchange", "e2e.test.exchange", "--routing-key", "e2e.test.key", "--body", "test", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");
        result.StderrOutput.Should().Contain("Exchange:    e2e.test.exchange");
        result.StderrOutput.Should().Contain("Routing Key: e2e.test.key");
        
        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
        
        // Clean up
        await _helpers.DeleteBinding("e2e.test.exchange", TestQueue, "e2e.test.key");
        await _helpers.DeleteExchange("e2e.test.exchange");
    }

    [Fact]
    public async Task Publish_WithOutputSetToJson_ShouldShowResultAsJson()
    {
        // Act
        var result = await _helpers.RunRmqCommand(["publish", "--queue", TestQueue, "--body", "test body", "--output", "json", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Validate JSON output
        var jsonOutput = result.StderrOutput.Trim();
        var publishResponse = System.Text.Json.JsonSerializer.Deserialize(jsonOutput, JsonSerializationContext.RelaxedEscaping.PublishResponse);
        publishResponse.Should().NotBeNull();
        publishResponse.Status.Should().Be("success");
        publishResponse.Result.Should().NotBeNull();
        publishResponse.Result.MessagesPublished.Should().Be(1);
    }
}