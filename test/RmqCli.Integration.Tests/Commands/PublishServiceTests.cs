using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RmqCli.Commands.Publish;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared.Output;

namespace RmqCli.Integration.Tests.Commands;

public class PublishServiceTests
{
    public class PublishMessagePlainText
    {
        [Fact]
        public async Task PublishesPlainTextMessage_Successfully()
        {
            // Arrange
            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                MessageBody = "Hello, RabbitMQ!",
                BurstCount = 1
            });

            SetupSuccessfulPublish(mocks);

            // Act
            var result = await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            result.Should().Be(0);
            await mocks.Channel.Received(1).BasicPublishAsync(
                Arg.Is<string>(e => e == string.Empty),
                Arg.Is<string>(rk => rk == "test-queue"),
                Arg.Is<bool>(m => m == true),
                Arg.Is<BasicProperties>(p => p.MessageId!.StartsWith("msg-")),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SetsTimestamp_InUtc()
        {
            // Arrange
            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                MessageBody = "Test message",
                BurstCount = 1
            });

            SetupSuccessfulPublish(mocks);

            var beforePublish = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Act
            await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            var afterPublish = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Assert
            await mocks.Channel.Received(1).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Is<BasicProperties>(p =>
                    p.Timestamp.UnixTime >= beforePublish &&
                    p.Timestamp.UnixTime <= afterPublish),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task AppliesCliProperties_ToMessage()
        {
            // Arrange
            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                MessageBody = "Test",
                BurstCount = 1,
                ContentType = "text/plain",
                CorrelationId = "corr-123",
                DeliveryMode = DeliveryModes.Persistent,
                Priority = 5
            });

            SetupSuccessfulPublish(mocks);

            // Act
            await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            await mocks.Channel.Received(1).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Is<BasicProperties>(p =>
                    p.ContentType == "text/plain" &&
                    p.CorrelationId == "corr-123" &&
                    p.DeliveryMode == DeliveryModes.Persistent &&
                    p.Priority == 5),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task AppliesCliHeaders_ToMessage()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "x-custom-header", "test-value" },
                { "x-retry-count", 3 }
            };

            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                MessageBody = "Test",
                BurstCount = 1,
                Headers = headers
            });

            SetupSuccessfulPublish(mocks);

            // Act
            await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            await mocks.Channel.Received(1).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Is<BasicProperties>(p =>
                    p.Headers != null &&
                    p.Headers.ContainsKey("x-custom-header") &&
                    p.Headers.ContainsKey("x-retry-count")),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class PublishMessageInlineJson
    {
        [Fact]
        public async Task ParsesAndPublishesJsonMessage_Successfully()
        {
            // Arrange
            var jsonMessage = """
                {
                    "body": "Hello from JSON",
                    "properties": {
                        "contentType": "application/json",
                        "correlationId": "json-123"
                    },
                    "headers": {
                        "x-source": "test"
                    }
                }
                """;

            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                JsonMessage = jsonMessage,
                BurstCount = 1
            });

            SetupSuccessfulPublish(mocks);

            // Act
            var result = await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            result.Should().Be(0);

            await mocks.Channel.Received(1).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Is<BasicProperties>(p =>
                    p.ContentType == "application/json" &&
                    p.CorrelationId == "json-123" &&
                    p.Headers != null &&
                    p.Headers.ContainsKey("x-source")),
                Arg.Is<ReadOnlyMemory<byte>>(body =>
                    Encoding.UTF8.GetString(body.ToArray()) == "Hello from JSON"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MergesCliProperties_WithJsonMessage()
        {
            // Arrange
            var jsonMessage = """
                {
                    "body": "Test body",
                    "properties": {
                        "contentType": "application/json"
                    }
                }
                """;

            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                JsonMessage = jsonMessage,
                BurstCount = 1,
                // CLI property should override JSON property
                ContentType = "text/plain",
                Priority = 5
            });

            SetupSuccessfulPublish(mocks);

            // Act
            await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            await mocks.Channel.Received(1).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Is<BasicProperties>(p =>
                    p.ContentType == "text/plain" && // CLI overrides JSON
                    p.Priority == 5), // CLI adds new property
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ReturnsError_WhenJsonParsingFails()
        {
            // Arrange
            var invalidJson = "{ invalid json ]}";

            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                JsonMessage = invalidJson,
                BurstCount = 1
            });

            // Act
            var result = await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            result.Should().Be(1);
            mocks.StatusOutput.Received(1).ShowError(
                Arg.Is<string>(s => s.Contains("Failed to parse inline JSON message")),
                Arg.Any<ErrorInfo>());

            await mocks.Channel.DidNotReceive().BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class PublishMessageFromFilePlainText
    {
        [Fact]
        public async Task PublishesMultipleMessages_FromDelimitedFile()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "Message 1\nMessage 2\nMessage 3");

            try
            {
                var (service, mocks) = CreatePublishService();
                SetupSuccessfulPublish(mocks);

                // Act
                var result = await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 1);

                // Assert
                result.Should().Be(0);
                await mocks.Channel.Received(3).BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task RespectsMessageDelimiter_FromFileConfig()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "Message 1|Message 2|Message 3");

            try
            {
                var fileConfig = new FileConfig { MessageDelimiter = "|" };
                var (service, mocks) = CreatePublishService(fileConfig: fileConfig);
                SetupSuccessfulPublish(mocks);

                // Act
                await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 1);

                // Assert - Should publish 3 messages
                await mocks.Channel.Received(3).BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task SkipsEmptyLines_InDelimitedFile()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "Message 1\n\n\nMessage 2\n   \nMessage 3");

            try
            {
                var (service, mocks) = CreatePublishService();
                SetupSuccessfulPublish(mocks);

                // Act
                await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 1);

                // Assert - Should only publish 3 non-empty messages
                await mocks.Channel.Received(3).BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }

    public class PublishMessageFromFileNdjson
    {
        [Fact]
        public async Task ParsesAndPublishesNdjsonMessages_Successfully()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            var ndjson = """
                {"body": "Message 1", "properties": {"contentType": "application/json"}}
                {"body": "Message 2", "properties": {"priority": 5}}
                {"body": "Message 3"}
                """;
            await File.WriteAllTextAsync(tempFile, ndjson);

            try
            {
                var (service, mocks) = CreatePublishService();
                SetupSuccessfulPublish(mocks);

                // Act
                var result = await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 1);

                // Assert
                result.Should().Be(0);
                await mocks.Channel.Received(3).BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task MergesCliProperties_WithNdjsonMessages()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            var ndjson = """
                {"body": "Message 1", "properties": {"contentType": "application/json"}}
                """;
            await File.WriteAllTextAsync(tempFile, ndjson);

            try
            {
                var (service, mocks) = CreatePublishService(options: new PublishOptions
                {
                    Destination = new DestinationInfo { Queue = "test-queue" },
                    BurstCount = 1,
                    Priority = 9 // CLI property should be added
                });
                SetupSuccessfulPublish(mocks);

                // Act
                await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 1);

                // Assert
                await mocks.Channel.Received(1).BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Is<BasicProperties>(p =>
                        p.ContentType == "application/json" && // From JSON
                        p.Priority == 9), // From CLI
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task FallbacksToPlainText_WhenJsonParsingFails()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "Not JSON\nPlain text message");

            try
            {
                var (service, mocks) = CreatePublishService();
                SetupSuccessfulPublish(mocks);

                // Act
                var result = await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 1);

                // Assert - Should fall back to plain text mode and publish 2 messages
                result.Should().Be(0);
                await mocks.Channel.Received(2).BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }

    public class PublishMessageFromStdinTests
    {
        [Fact]
        public async Task PublishesPlainTextMessages_FromStdin()
        {
            // Arrange
            var stdin = new StringReader("Message 1\nMessage 2");
            Console.SetIn(stdin);

            var (service, mocks) = CreatePublishService();
            SetupSuccessfulPublish(mocks);

            // Act
            var result = await service.PublishMessageFromStdin(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            result.Should().Be(0);
            await mocks.Channel.Received(2).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishesNdjsonMessages_FromStdin()
        {
            // Arrange
            var ndjson = """
                {"body": "Message 1", "properties": {"contentType": "application/json"}}
                {"body": "Message 2"}
                """;
            var stdin = new StringReader(ndjson);
            Console.SetIn(stdin);

            var (service, mocks) = CreatePublishService();
            SetupSuccessfulPublish(mocks);

            // Act
            var result = await service.PublishMessageFromStdin(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            result.Should().Be(0);
            await mocks.Channel.Received(2).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task FallbacksToPlainText_WhenStdinIsNotJson()
        {
            // Arrange
            var stdin = new StringReader("Plain text message\nAnother message");
            Console.SetIn(stdin);

            var (service, mocks) = CreatePublishService();
            SetupSuccessfulPublish(mocks);

            // Act
            var result = await service.PublishMessageFromStdin(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            result.Should().Be(0);
            await mocks.Channel.Received(2).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class BurstModeAndMessageIds
    {
        [Fact]
        public async Task PublishesMessageMultipleTimes_InBurstMode()
        {
            // Arrange
            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                MessageBody = "Burst message",
                BurstCount = 5
            });

            SetupSuccessfulPublish(mocks);

            // Act
            await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 5);

            // Assert - Should publish 5 times (1 message × 5 burst)
            await mocks.Channel.Received(5).BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GeneratesUniqueMessageIds_ForEachBurstIteration()
        {
            // Arrange
            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                MessageBody = "Test",
                BurstCount = 3
            });

            var messageIds = new List<string>();
            mocks.Channel.BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Do<BasicProperties>(p => messageIds.Add(p.MessageId!)),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
                .Returns(ValueTask.CompletedTask);

            // Act
            await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 3);

            // Assert
            messageIds.Should().HaveCount(3);
            messageIds.Should().OnlyHaveUniqueItems();
            messageIds.Should().AllSatisfy(id => id.Should().StartWith("msg-"));
        }

        [Fact]
        public async Task GeneratesSequentialMessageIds_ForMultipleMessages()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "Message 1\nMessage 2\nMessage 3");

            try
            {
                var (service, mocks) = CreatePublishService();

                var messageIds = new List<string>();
                mocks.Channel.BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Do<BasicProperties>(p => messageIds.Add(p.MessageId!)),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>())
                    .Returns(ValueTask.CompletedTask);

                // Act
                await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 1);

                // Assert
                messageIds.Should().HaveCount(3);
                messageIds.Should().OnlyHaveUniqueItems();

                // All message IDs should share the same base (msg-xxxxx)
                // with sequential suffixes (-1, -2, -3)
                var baseId = messageIds[0].Substring(0, messageIds[0].LastIndexOf('-'));
                messageIds[0].Should().Be($"{baseId}-1");
                messageIds[1].Should().Be($"{baseId}-2");
                messageIds[2].Should().Be($"{baseId}-3");
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task GeneratesCorrectMessageIds_WithBurstAndMultipleMessages()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "Message 1\nMessage 2");

            try
            {
                var (service, mocks) = CreatePublishService();

                var messageIds = new List<string>();
                mocks.Channel.BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Do<BasicProperties>(p => messageIds.Add(p.MessageId!)),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>())
                    .Returns(ValueTask.CompletedTask);

                // Act
                await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 2);

                // Assert - 2 messages × 2 burst = 4 total publishes
                messageIds.Should().HaveCount(4);
                messageIds.Should().OnlyHaveUniqueItems();

                // Expected pattern: msg-xxxxx-1-1, msg-xxxxx-1-2, msg-xxxxx-2-1, msg-xxxxx-2-2
                var baseId = messageIds[0].Split('-')[0..3];
                var baseIdStr = string.Join("-", baseId);

                messageIds[0].Should().Be($"{baseIdStr}-1-1");
                messageIds[1].Should().Be($"{baseIdStr}-1-2");

                // Second message uses same base but different message suffix
                messageIds[2].Should().EndWith("-2-1");
                messageIds[3].Should().EndWith("-2-2");
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }

    public class ErrorHandling
    {
        [Fact]
        public async Task HandlesExchangeNotFound_WithAlreadyClosedException()
        {
            // Arrange
            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Exchange = "nonexistent", RoutingKey = "test" },
                MessageBody = "Test",
                BurstCount = 1
            });

            var shutdownReason = new ShutdownEventArgs(
                ShutdownInitiator.Library,
                404,
                "not found - no exchange 'nonexistent' in vhost '/'"); // lowercase to match Contains check

            mocks.Channel.BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
                .Returns(_ => ValueTask.FromException(new AlreadyClosedException(shutdownReason)));

            // Act
            var result = await service.PublishMessage(
                new DestinationInfo { Exchange = "nonexistent", RoutingKey = "test" },
                burstCount: 1);

            // Assert
            result.Should().Be(1);
            mocks.StatusOutput.Received(1).ShowError(
                Arg.Is<string>(s => s.Contains("Failed to publish")),
                Arg.Is<ErrorInfo>(e => e.Error.Contains("Exchange not found")));

            // Verify WritePublishResult was NOT called (since we had an error)
            mocks.ResultOutput.DidNotReceive().WritePublishResult(Arg.Any<PublishResponse>());
        }

        [Fact]
        public async Task HandlesMaxSizeExceeded_WithAlreadyClosedException()
        {
            // Arrange
            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                MessageBody = "Large message",
                BurstCount = 1
            });

            var shutdownReason = new ShutdownEventArgs(
                ShutdownInitiator.Library,
                406,
                "PRECONDITION_FAILED - max size exceeded");

            mocks.Channel.BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
                .Returns(_ => ValueTask.FromException(new AlreadyClosedException(shutdownReason)));

            // Act
            var result = await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            result.Should().Be(1);
            mocks.StatusOutput.Received(1).ShowError(
                Arg.Is<string>(s => s.Contains("Failed to publish")),
                Arg.Is<ErrorInfo>(e => e.Error.Contains("exceeds maximum allowed size")));

            // Verify WritePublishResult was NOT called (since we had an error)
            mocks.ResultOutput.DidNotReceive().WritePublishResult(Arg.Any<PublishResponse>());
        }

        // Note: PublishException tests are omitted because PublishException doesn't have
        // a parameterless constructor and can't be easily mocked with NSubstitute.
        // These scenarios would be better tested in E2E tests with a real RabbitMQ instance.
    }

    public class CancellationTests
    {
        [Fact]
        public async Task HandlesOperationCanceled_WithPartialResults()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "Message 1\nMessage 2\nMessage 3\nMessage 4\nMessage 5");

            try
            {
                var (service, mocks) = CreatePublishService();

                var publishCount = 0;
                mocks.Channel.BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>())
                    .Returns(_ =>
                    {
                        publishCount++;
                        if (publishCount >= 3)
                        {
                            throw new OperationCanceledException();
                        }
                        return ValueTask.CompletedTask;
                    });

                // Act
                await service.PublishMessageFromFile(
                    new DestinationInfo { Queue = "test-queue" },
                    new FileInfo(tempFile),
                    burstCount: 1);

                // Assert
                publishCount.Should().Be(3);
                mocks.StatusOutput.Received(1).ShowWarning(
                    Arg.Is<string>(s => s.Contains("cancelled")),
                    Arg.Any<bool>());

                mocks.ResultOutput.Received(1).WritePublishResult(
                    Arg.Is<PublishResponse>(r => r.Status == "partial"));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task HandlesOperationCanceled_WithNoSuccessfulPublishes()
        {
            // Arrange
            var (service, mocks) = CreatePublishService(options: new PublishOptions
            {
                Destination = new DestinationInfo { Queue = "test-queue" },
                MessageBody = "Test",
                BurstCount = 1
            });

            mocks.Channel.BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
                .Returns(_ => throw new OperationCanceledException());

            // Act
            await service.PublishMessage(
                new DestinationInfo { Queue = "test-queue" },
                burstCount: 1);

            // Assert
            mocks.StatusOutput.Received(1).ShowError(
                Arg.Is<string>(s => s.Contains("No messages were published")),
                Arg.Any<ErrorInfo?>());

            mocks.ResultOutput.Received(1).WritePublishResult(
                Arg.Is<PublishResponse>(r => r.Status == "failure"));
        }
    }

    #region Test Helpers

    private static (PublishService service, MockDependencies mocks) CreatePublishService(
        PublishOptions? options = null,
        FileConfig? fileConfig = null)
    {
        var channelFactory = Substitute.For<IRabbitChannelFactory>();
        var channel = Substitute.For<IChannel>();
        var statusOutput = Substitute.For<IStatusOutputService>();
        var resultOutput = Substitute.For<IPublishOutputService>();
        var logger = new NullLogger<PublishService>();

        channelFactory.GetChannelWithPublisherConfirmsAsync()
            .Returns(channel);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channelFactory.CloseConnectionAsync()
            .Returns(Task.CompletedTask);

        // Always set up ExecuteWithProgress to actually execute the workload
        statusOutput.ExecuteWithProgress(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Func<IProgress<int>, Task>>())
            .Returns(callInfo =>
            {
                var workload = callInfo.Arg<Func<IProgress<int>, Task>>();
                return workload(null!); // Execute the workload
            });

        var config = fileConfig ?? new FileConfig { MessageDelimiter = "\n" };
        var publishOptions = options ?? new PublishOptions
        {
            Destination = new DestinationInfo { Queue = "test-queue" },
            BurstCount = 1
        };

        var service = new PublishService(
            channelFactory,
            logger,
            config,
            statusOutput,
            resultOutput,
            publishOptions);

        return (service, new MockDependencies
        {
            ChannelFactory = channelFactory,
            Channel = channel,
            StatusOutput = statusOutput,
            ResultOutput = resultOutput
        });
    }

    private static void SetupSuccessfulPublish(MockDependencies mocks)
    {
        mocks.Channel.BasicPublishAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
    }

    private record MockDependencies
    {
        public required IRabbitChannelFactory ChannelFactory { get; init; }
        public required IChannel Channel { get; init; }
        public required IStatusOutputService StatusOutput { get; init; }
        public required IPublishOutputService ResultOutput { get; init; }
    }
    
    #endregion
}
