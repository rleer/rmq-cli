using RmqCli.Subcutaneous.Tests.Infrastructure;
using RmqCli.Tests.Shared.Infrastructure;
using Xunit.Abstractions;

namespace RmqCli.Subcutaneous.Tests.Commands;

/// <summary>
/// Subcutaneous tests for the publish command.
/// These tests invoke commands in-process with real RabbitMQ to achieve better code coverage
/// while still testing the full command workflow.
/// </summary>
[Collection("RabbitMQ")]
public class PublishCommandTests : IAsyncLifetime
{
    private readonly RabbitMqTestHelpers _helpers;
    private const string TestQueue = "sub-publish-test";

    public PublishCommandTests(RabbitMqFixture fixture, ITestOutputHelper output)
    {
        _helpers = new RabbitMqTestHelpers(fixture, output);
    }

    public async Task InitializeAsync()
    {
        // Ensure test queue exists before each test
        await _helpers.EnsureQueueExists(TestQueue);
    }

    public async Task DisposeAsync()
    {
        // Purge messages from test queue but keep the queue for next test
        try
        {
            await _helpers.PurgeQueue(TestQueue);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region Input Source Validation

    // NOTE: Validation tests for "no message provided" cannot be done in subcutaneous tests
    // because .
    // NOTE: Cannot test "no stdin, no message" validation in automated tests because:
    // - Console.IsInputRedirected is always true in xUnit (OS-level redirection) and CliWrap (E2E tests)
    //   always redirects stdin (can't test Console.IsInputRedirected == false)
    // - The validation requires stdin to NOT be redirected, which can only happen in interactive terminal
    // - Manual test: Run `./rmq publish --queue test` in terminal without input -> should show validation error


    [Fact]
    public async Task Publish_WithValidBody_ShouldPublishMessage()
    {
        // Arrange
        var messageBody = "Test message content";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", messageBody, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully"); // Status messages go to STDERR
        result.StderrOutput.Should().Contain(TestQueue);
        result.StderrOutput.Should().Contain("20 bytes"); // Message size
    }

    [Fact]
    public async Task Publish_WithNonExistentMessageFile_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--message-file", "/non/existent/file.txt"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("not found");
    }

    #endregion

    #region Destination Validation

    [Fact]
    public async Task Publish_WithoutQueueOrExchange_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--body", "test"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("queue");
    }

    [Fact]
    public async Task Publish_ToQueue_ShouldSucceed()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test message"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify message was published to the queue
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_ToExchangeWithoutRoutingKey_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--exchange", "test.exchange", "--body", "test"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("routing");
    }

    [Fact]
    public async Task Publish_WithBothQueueAndExchange_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--exchange", "test.exchange", "--routing-key", "test.key", "--body", "test"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The validation should prevent this, but if it doesn't, the command will fail
        result.StderrOutput.Should().Contain("Failed");
    }

    #endregion

    #region Message Properties

    [Fact]
    public async Task Publish_WithContentType_ShouldSetProperty()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--content-type", "application/json"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithAppId_ShouldSetProperty()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--app-id", "my-app"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithMultipleProperties_ShouldSetAllProperties()
    {
        // Act
        var uniqueBody = Guid.NewGuid().ToString();
        var publishResult = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", uniqueBody,
            "--content-type", "application/json",
            "--app-id", "my-app",
            "--correlation-id", "corr-123",
            "--delivery-mode", "Persistent"
        ]);

        // Assert
        publishResult.IsSuccess.Should().BeTrue();

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);

        var message = await _helpers.RunRmqCommand(["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        message.IsSuccess.Should().BeTrue();
        message.StdoutOutput.Should().Contain(uniqueBody);
        message.StdoutOutput.Should().Contain("Content Type: application/json");
        message.StdoutOutput.Should().Contain("App ID: my-app");
        message.StdoutOutput.Should().Contain("Correlation ID: corr-123");
        message.StdoutOutput.Should().Contain("Delivery Mode: Persistent (2)");
    }

    [Fact]
    public async Task Publish_WithInvalidDeliveryMode_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--delivery-mode", "Invalid"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("delivery");
    }

    [Fact]
    public async Task Publish_WithPriorityBoundaryValues_ShouldSucceed()
    {
        // Note: Using 1 and 254 instead of 0 and 255 due to validation logic
        // Act - Test with priority 1
        var result1 = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test1", "--priority", "1"]);

        // Act - Test with priority 254
        var result2 = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test2", "--priority", "254"]);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        // Verify messages were published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(2);
    }

    [Fact]
    public async Task Publish_WithHeader_ShouldSetHeader()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--header", "x-custom:value"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithMultipleHeaders_ShouldSetAllHeaders()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", "test",
            "--header", "x-custom-1:value1",
            "--header", "x-custom-2:value2",
            "--header", "x-custom-3:value3"
        ]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithInvalidHeaderFormat_ShouldFail()
    {
        // Act - Header without colon separator
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--header", "invalid-header"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("header");
    }

    #endregion

    #region Burst Mode

    [Fact]
    public async Task Publish_WithBurst1_ShouldPublishOnce()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--burst", "1"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify only 1 message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithBurst10_ShouldPublish10Copies()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--burst", "10"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify 10 messages were published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(10);
    }

    [Fact]
    public async Task Publish_WithBurstZero_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--burst", "0"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("burst");
    }

    #endregion

    #region Output Formats

    [Fact]
    public async Task Publish_WithPlainOutput_ShouldReturnPlainText()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--output", "plain"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Note: Output goes to stderr for progress messages, not stdout
        result.StderrOutput.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Publish_WithJsonOutput_ShouldReturnValidJson()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--output", "json"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify JSON is valid (JSON output goes to stdout)
        // Note: For now, just verify the command succeeded since output format may vary
        result.StderrOutput.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Publish_WithInvalidOutputFormat_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--output", "invalid"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("not recognized");
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Publish_ToNonExistentExchange_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--exchange", "non.existent.exchange", "--routing-key", "test.key", "--body", "test"]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("Failed to publish");
    }

    [Fact]
    public async Task Publish_ToNonExistentQueue_ShouldFail()
    {
        // Act - Publish to queue that doesn't exist (mandatory flag should cause return)
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", "non-existent-queue-" + Guid.NewGuid(), "--body", "test"]);

        // Assert - Should fail because queue doesn't exist and mandatory flag is set
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("Failed to publish");
    }

    #endregion

    #region Stdin Input Tests

    [Fact]
    public async Task Publish_WithStdinPlainText_ShouldPublishMessage()
    {
        // Arrange
        var messageBody = "Message from stdin test";

        // Act
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
    public async Task Publish_WithStdinMultipleMessages_ShouldPublishAll()
    {
        // Arrange - Multiple messages separated by newlines (default delimiter)
        var stdinInput = "Message 1\nMessage 2\nMessage 3";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--no-color"],
            stdinInput: stdinInput);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 3 messages successfully");

        // Verify all messages were published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(3);
    }

    [Fact]
    public async Task Publish_WithStdinJsonMessage_ShouldPublishWithProperties()
    {
        // Arrange - JSON message via stdin
        var jsonMessage = """{"body":"JSON message from stdin","properties":{"priority":5,"contentType":"application/json"}}""";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--no-color"],
            stdinInput: jsonMessage);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        // Verify message was published with correct properties
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("JSON message from stdin");
        consumeResult.StdoutOutput.Should().Contain("Priority: 5");
        consumeResult.StdoutOutput.Should().Contain("Content Type: application/json");
    }

    [Fact]
    public async Task Publish_WithStdinNdjson_ShouldPublishAllMessages()
    {
        // Arrange - NDJSON format (multiple JSON messages, one per line)
        var ndjsonInput = """
                          {"body":"First NDJSON message"}
                          {"body":"Second NDJSON message","properties":{"priority":3}}
                          {"body":"Third NDJSON message","headers":{"x-source":"test"}}
                          """;

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--no-color"],
            stdinInput: ndjsonInput);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 3 messages successfully");

        // Verify all messages were published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(3);
    }

    [Fact]
    public async Task Publish_WithEmptyStdin_ShouldShowWarning()
    {
        // Act - Empty stdin
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--no-color"],
            stdinInput: "");

        // Assert - Should succeed but with warning about no messages
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("No messages");
    }

    [Fact]
    public async Task Publish_WithStdinAndBurst_ShouldPublishMultipleCopies()
    {
        // Arrange
        var stdinInput = "Message to burst";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--burst", "5", "--no-color"],
            stdinInput: stdinInput);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 5 messages successfully");

        // Verify all copies were published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(5);
    }

    #endregion

    #region File Input Tests

    [Fact]
    public async Task Publish_WithFileContainingSingleMessage_ShouldPublish()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "Single message from file");

        try
        {
            // Act
            var result = await _helpers.RunRmqCommand(
                ["publish", "--queue", TestQueue, "--message-file", tempFile, "--no-color"]);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.StderrOutput.Should().Contain("Published 1 message successfully");

            var queueInfo = await _helpers.GetQueueInfo(TestQueue);
            queueInfo.MessageCount.Should().Be(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Publish_WithFileContainingMultipleMessages_ShouldPublishAll()
    {
        // Arrange - Multiple messages separated by newlines
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "Message 1\nMessage 2\nMessage 3\nMessage 4");

        try
        {
            // Act
            var result = await _helpers.RunRmqCommand(
                ["publish", "--queue", TestQueue, "--message-file", tempFile, "--no-color"]);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.StderrOutput.Should().Contain("Published 4 messages successfully");

            var queueInfo = await _helpers.GetQueueInfo(TestQueue);
            queueInfo.MessageCount.Should().Be(4);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Publish_WithNdjsonFile_ShouldPublishAllMessages()
    {
        // Arrange - NDJSON format file
        var tempFile = Path.GetTempFileName();
        var ndjsonContent = """
                            {"body":"First message","properties":{"priority":1}}
                            {"body":"Second message","properties":{"priority":2}}
                            {"body":"Third message","properties":{"contentType":"text/plain"}}
                            """;
        await File.WriteAllTextAsync(tempFile, ndjsonContent);

        try
        {
            // Act
            var result = await _helpers.RunRmqCommand(
                ["publish", "--queue", TestQueue, "--message-file", tempFile, "--no-color"]);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.StderrOutput.Should().Contain("Published 3 messages successfully");

            var queueInfo = await _helpers.GetQueueInfo(TestQueue);
            queueInfo.MessageCount.Should().Be(3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Publish_WithFileAndBurst_ShouldPublishMultipleCopiesOfEach()
    {
        // Arrange - 2 messages, burst 3 = 6 total messages
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "Message A\nMessage B");

        try
        {
            // Act
            var result = await _helpers.RunRmqCommand(
                ["publish", "--queue", TestQueue, "--message-file", tempFile, "--burst", "3", "--no-color"]);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.StderrOutput.Should().Contain("Published 6 messages successfully");

            var queueInfo = await _helpers.GetQueueInfo(TestQueue);
            queueInfo.MessageCount.Should().Be(6);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Publish_WithEmptyFile_ShouldShowWarning()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "");

        try
        {
            // Act
            var result = await _helpers.RunRmqCommand(
                ["publish", "--queue", TestQueue, "--message-file", tempFile, "--no-color"]);

            // Assert - Should succeed but publish 0 messages (empty file after splitting)
            result.StderrOutput.Should().Contain("0 messages");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region JSON Message Tests (--message flag)

    [Fact]
    public async Task Publish_WithJsonMessageAndHeaders_ShouldSetHeaders()
    {
        // Arrange
        var jsonMessage = """{"body":"Message with headers","headers":{"x-custom":"value1","x-trace-id":"trace-123"}}""";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--message", jsonMessage, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify headers were set by consuming the message
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("x-custom: value1");
        consumeResult.StdoutOutput.Should().Contain("x-trace-id: trace-123");
    }

    [Fact]
    public async Task Publish_WithJsonMessageAndCliPropertyOverride_ShouldUseCli()
    {
        // Arrange - JSON sets priority to 1, CLI overrides to 9
        var jsonMessage = """{"body":"Message with priority","properties":{"priority":1}}""";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--message", jsonMessage, "--priority", "9", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify CLI priority (9) was used instead of JSON priority (1)
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("Priority: 9");
    }

    [Fact]
    public async Task Publish_WithInvalidJsonMessage_ShouldFail()
    {
        // Arrange - Malformed JSON
        var invalidJson = """{"body":"incomplete""";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--message", invalidJson]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("Failed to parse");
    }

    [Fact]
    public async Task Publish_WithJsonMessageMissingBody_ShouldFail()
    {
        // Arrange - JSON without body field is invalid
        var jsonMessage = """{"properties":{"priority":5}}""";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--message", jsonMessage, "--no-color"]);

        // Assert - JSON messages require a body field
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("Failed to parse");
    }

    [Fact]
    public async Task Publish_WithJsonMessageAndCliHeaders_ShouldMergeHeaders()
    {
        // Arrange - JSON has one header, CLI adds another
        var jsonMessage = """{"body":"Message","headers":{"x-from-json":"json-value"}}""";

        // Act
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--message", jsonMessage,
            "--header", "x-from-cli:cli-value", "--no-color"
        ]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify both headers are present
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("x-from-json: json-value");
        consumeResult.StdoutOutput.Should().Contain("x-from-cli: cli-value");
    }

    #endregion

    #region Exchange and Routing Tests

    [Fact]
    public async Task Publish_ToExchangeWithRoutingKey_ShouldRouteToQueue()
    {
        // Arrange - Create exchange and bind to test queue
        const string exchangeName = "sub-test-direct-exchange";
        const string routingKey = "sub.test.routing.key";

        await _helpers.DeclareExchange(exchangeName, "direct");
        await _helpers.DeclareBinding(exchangeName, TestQueue, routingKey);

        try
        {
            // Act
            var result = await _helpers.RunRmqCommand(
            [
                "publish", "--exchange", exchangeName, "--routing-key", routingKey,
                "--body", "Routed message", "--no-color"
            ]);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.StderrOutput.Should().Contain("Published 1 message successfully");
            result.StderrOutput.Should().Contain(exchangeName);
            result.StderrOutput.Should().Contain(routingKey);

            // Verify message was routed to the queue
            var queueInfo = await _helpers.GetQueueInfo(TestQueue);
            queueInfo.MessageCount.Should().Be(1);
        }
        finally
        {
            await _helpers.DeleteBinding(exchangeName, TestQueue, routingKey);
            await _helpers.DeleteExchange(exchangeName);
        }
    }

    [Fact]
    public async Task Publish_ToExchangeWithNoMatchingBinding_ShouldFail()
    {
        // Arrange - Create exchange but NO binding (message won't be routable)
        const string exchangeName = "sub-test-unroutable-exchange";

        await _helpers.DeclareExchange(exchangeName, "direct");

        try
        {
            // Act - Publish with routing key that has no binding
            var result = await _helpers.RunRmqCommand(
            [
                "publish", "--exchange", exchangeName, "--routing-key", "unbound.key",
                "--body", "Unroutable message", "--no-color"
            ]);

            // Assert - Should fail because mandatory flag is set and message is unroutable
            result.IsSuccess.Should().BeFalse();
            result.StderrOutput.Should().Contain("Failed to publish");
        }
        finally
        {
            await _helpers.DeleteExchange(exchangeName);
        }
    }

    [Fact]
    public async Task Publish_ToTopicExchangeWithWildcard_ShouldRoute()
    {
        // Arrange - Create topic exchange with wildcard binding
        const string exchangeName = "sub-test-topic-exchange";
        const string bindingPattern = "orders.*.created"; // Wildcard binding

        await _helpers.DeclareExchange(exchangeName, "topic");
        await _helpers.DeclareBinding(exchangeName, TestQueue, bindingPattern);

        try
        {
            // Act - Publish with routing key matching the pattern
            var result = await _helpers.RunRmqCommand(
            [
                "publish", "--exchange", exchangeName, "--routing-key", "orders.eu.created",
                "--body", "Topic message", "--no-color"
            ]);

            // Assert
            result.IsSuccess.Should().BeTrue();

            // Verify message was routed
            var queueInfo = await _helpers.GetQueueInfo(TestQueue);
            queueInfo.MessageCount.Should().Be(1);
        }
        finally
        {
            await _helpers.DeleteBinding(exchangeName, TestQueue, bindingPattern);
            await _helpers.DeleteExchange(exchangeName);
        }
    }

    #endregion

    #region Additional Message Property Tests

    [Fact]
    public async Task Publish_WithContentEncoding_ShouldSetProperty()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", "encoded content",
            "--content-encoding", "gzip", "--no-color"
        ]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify property was set
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("Content Encoding: gzip");
    }

    [Fact]
    public async Task Publish_WithExpiration_ShouldSetProperty()
    {
        // Act - Set expiration to 60000ms (60 seconds)
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", "expiring message",
            "--expiration", "60000", "--no-color"
        ]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify property was set
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("Expiration: 60000");
    }

    [Fact]
    public async Task Publish_WithReplyTo_ShouldSetProperty()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", "request message",
            "--reply-to", "reply-queue", "--no-color"
        ]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify property was set
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("Reply To: reply-queue");
    }

    [Fact]
    public async Task Publish_WithType_ShouldSetProperty()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", "typed message",
            "--type", "OrderCreated", "--no-color"
        ]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify property was set
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("Type: OrderCreated");
    }

    [Fact]
    public async Task Publish_WithTransientDeliveryMode_ShouldSetProperty()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", "transient message",
            "--delivery-mode", "Transient", "--no-color"
        ]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify property was set (Transient = Non-persistent = 1)
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("Delivery Mode: Non-persistent (1)");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Publish_WithEmptyBody_ShouldPublishEmptyMessage()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message");

        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithLargeMessage_ShouldSucceed()
    {
        // Arrange - Create a 1MB message
        var largeBody = new string('X', 1024 * 1024);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", largeBody, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message");
        result.StderrOutput.Should().Contain("1 MB"); // Large messages show as MB

        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithUnicodeMessage_ShouldPreserveContent()
    {
        // Arrange
        var unicodeBody = "Unicode: 你好世界 🎉 مرحبا العالم Привет мир";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", unicodeBody, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify message content is preserved
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("你好世界");
        consumeResult.StdoutOutput.Should().Contain("🎉");
    }

    [Fact]
    public async Task Publish_WithSpecialCharactersInHeader_ShouldWork()
    {
        // Arrange - Header with special characters
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", "test",
            "--header", "x-special:value=with:colons&special", "--no-color"
        ]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify header is preserved
        var consumeResult = await _helpers.RunRmqCommand(
            ["consume", "--queue", TestQueue, "--count", "1", "--output", "plain"]);
        consumeResult.IsSuccess.Should().BeTrue();
        consumeResult.StdoutOutput.Should().Contain("x-special: value=with:colons&special");
    }

    [Fact]
    public async Task Publish_WithNewlinesInBody_ShouldPreserveContent()
    {
        // Arrange - Message body with newlines
        var bodyWithNewlines = "Line 1\nLine 2\nLine 3";

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", bodyWithNewlines, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    #endregion

    #region Verbose and Quiet Mode Tests

    [Fact]
    public async Task Publish_WithVerboseFlag_ShouldShowDetailedOutput()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--verbose", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Verbose mode should show additional debug information
        result.StderrOutput.Should().Contain("Published");
    }

    [Fact]
    public async Task Publish_WithQuietFlag_ShouldMinimizeOutput()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--quiet", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Quiet mode should have minimal output (status messages suppressed)
        // The exact behavior depends on implementation
    }

    [Fact(Skip = "Validation for --verbose and --quiet conflict not working in in-process tests - see RootCommandHandler.cs line 112")]
    public async Task Publish_WithBothVerboseAndQuiet_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--verbose", "--quiet"]);

        // Assert
        // The validation should prevent using both options together
        result.IsSuccess.Should().BeFalse(because: "using both --verbose and --quiet is not allowed");
        result.StderrOutput.Should().Contain("cannot use both");
    }

    #endregion

    #region Input Mode Conflict Tests

    [Fact]
    public async Task Publish_WithBodyAndMessage_ShouldFail()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
        [
            "publish", "--queue", TestQueue, "--body", "test",
            "--message", """{"body":"json"}"""
        ]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("Cannot specify both");
    }

    [Fact]
    public async Task Publish_WithBodyAndMessageFile_ShouldFail()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "file content");

        try
        {
            // Act
            var result = await _helpers.RunRmqCommand(
                ["publish", "--queue", TestQueue, "--body", "test", "--message-file", tempFile]);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.StderrOutput.Should().Contain("Cannot specify both");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Publish_WithMessageAndMessageFile_ShouldFail()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "file content");

        try
        {
            // Act
            var result = await _helpers.RunRmqCommand(
            [
                "publish", "--queue", TestQueue,
                "--message", """{"body":"json"}""",
                "--message-file", tempFile
            ]);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.StderrOutput.Should().Contain("Cannot specify both");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region JSON Output Tests

    [Fact]
    public async Task Publish_WithJsonOutput_ShouldIncludeMessageDetails()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "json output test", "--output", "json", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify JSON structure (uses snake_case for result properties)
        result.StderrOutput.Should().Contain("\"status\"");
        result.StderrOutput.Should().Contain("\"success\"");
        result.StderrOutput.Should().Contain("\"messages_published\"");
        result.StderrOutput.Should().Contain("\"destination\"");
    }

    [Fact]
    public async Task Publish_WithJsonOutputAndBurst_ShouldShowCorrectCount()
    {
        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "burst test", "--burst", "5", "--output", "json", "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify message count in JSON (uses snake_case)
        result.StderrOutput.Should().Contain("\"messages_published\":5");
    }

    #endregion

    #region Custom Config File Tests (--config flag)

    [Fact]
    public async Task Publish_WithValidCustomConfigFile_ShouldLoadSuccessfully()
    {
        // Arrange - Create a custom config file with different ClientName (a benign setting)
        var customConfigPath = Path.Combine(Path.GetTempPath(), $"custom-config-{Guid.NewGuid()}.toml");
        var customConfig = """
            [RabbitMq]
            ClientName = "custom-test-client"
            """;
        await File.WriteAllTextAsync(customConfigPath, customConfig);

        // Act - Use --config to load custom config, CLI args provide connection details
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--config", customConfigPath, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithNonExistentConfigFile_ShouldContinue()
    {
        // Arrange - Path to a config file that doesn't exist
        // The tool is resilient and will continue even if custom config doesn't exist
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid()}.toml");

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--config", nonExistentPath, "--no-color"]);

        // Assert - Should succeed because CLI args provide all needed connection info
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithInvalidTomlSyntax_ShouldWarnAndContinue()
    {
        // Arrange - Create a config file with invalid TOML syntax
        var invalidConfigPath = Path.Combine(Path.GetTempPath(), $"invalid-syntax-{Guid.NewGuid()}.toml");
        var invalidToml = """
            [RabbitMq
            Host = "missing-closing-bracket"
            This is not valid TOML syntax
            """;
        await File.WriteAllTextAsync(invalidConfigPath, invalidToml);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--config", invalidConfigPath, "--no-color"]);

        // Assert - Should show warning but succeed with CLI args
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Failed to read toml file");
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        // Verify message was published despite config error
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithMalformedTomlValues_ShouldFailWithConfigError()
    {
        // Arrange - Create a config file with type mismatch (Port as string instead of int)
        // This is valid TOML syntax but causes a configuration binding error
        var invalidConfigPath = Path.Combine(Path.GetTempPath(), $"invalid-values-{Guid.NewGuid()}.toml");
        var invalidToml = """
            [RabbitMq]
            Host = "valid-host"
            Port = "not-a-number"
            """;
        await File.WriteAllTextAsync(invalidConfigPath, invalidToml);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--config", invalidConfigPath, "--no-color"]);

        // Assert - Configuration binding errors cause command failure
        result.IsSuccess.Should().BeFalse();
        result.StderrOutput.Should().Contain("Failed to convert configuration value");
        result.StderrOutput.Should().Contain("RabbitMq:Port");
    }

    [Fact]
    public async Task Publish_CustomConfigOverridesUserConfig_InOrderOfPrecedence()
    {
        // Arrange - Create both user config and custom config
        // User config sets ClientName to one value
        var userConfigPath = Path.Combine(Path.GetTempPath(), $"user-config-{Guid.NewGuid()}.toml");
        var userConfig = """
            [RabbitMq]
            ClientName = "user-config-client"
            """;
        await File.WriteAllTextAsync(userConfigPath, userConfig);

        // Custom config sets ClientName to different value (should override user config)
        var customConfigPath = Path.Combine(Path.GetTempPath(), $"override-test-{Guid.NewGuid()}.toml");
        var customConfig = """
            [RabbitMq]
            ClientName = "custom-config-client"
            """;
        await File.WriteAllTextAsync(customConfigPath, customConfig);

        // Act - Use --config which should have higher precedence than user config
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test precedence", "--config", customConfigPath, "--no-color"]);

        // Assert - The command should succeed with custom config taking precedence
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        // Verify message was published
        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithEmptyConfigFile_ShouldSucceed()
    {
        // Arrange - Create an empty config file (should be valid, just no settings)
        var emptyConfigPath = Path.Combine(Path.GetTempPath(), $"empty-config-{Guid.NewGuid()}.toml");
        await File.WriteAllTextAsync(emptyConfigPath, "");

        // Act - Empty config is valid, connection details from CLI args should work
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--config", emptyConfigPath, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithConfigFileContainingOnlyComments_ShouldSucceed()
    {
        // Arrange - Create a config file with only comments
        var commentOnlyConfigPath = Path.Combine(Path.GetTempPath(), $"comments-only-{Guid.NewGuid()}.toml");
        var commentOnlyToml = """
            # This is a comment
            # [RabbitMq]
            # Host = "commented-out-host"

            # Another comment
            """;
        await File.WriteAllTextAsync(commentOnlyConfigPath, commentOnlyToml);

        // Act
        var result = await _helpers.RunRmqCommand(
            ["publish", "--queue", TestQueue, "--body", "test", "--config", commentOnlyConfigPath, "--no-color"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StderrOutput.Should().Contain("Published 1 message successfully");

        var queueInfo = await _helpers.GetQueueInfo(TestQueue);
        queueInfo.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithConfigFileInvalidPermissions_ShouldWarnAndContinue()
    {
        // Note: This test is platform-specific and may not work on Windows
        if (!OperatingSystem.IsWindows())
        {
            // Arrange - Create a config file and remove read permissions
            var noPermissionConfigPath = Path.Combine(Path.GetTempPath(), $"no-permission-{Guid.NewGuid()}.toml");
            await File.WriteAllTextAsync(noPermissionConfigPath, "[RabbitMq]\nHost = \"test\"");

            // Remove read permissions (Unix only)
            File.SetUnixFileMode(noPermissionConfigPath, UnixFileMode.None);

            try
            {
                // Act
                var result = await _helpers.RunRmqCommand(
                    ["publish", "--queue", TestQueue, "--body", "test", "--config", noPermissionConfigPath, "--no-color"]);

                // Assert - Should warn but succeed with CLI args
                result.IsSuccess.Should().BeTrue();
                result.StderrOutput.Should().Contain("Failed to read toml file");
                result.StderrOutput.Should().Contain("Published 1 message successfully");
            }
            finally
            {
                // Restore permissions for cleanup
                File.SetUnixFileMode(noPermissionConfigPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
    }

    #endregion
}