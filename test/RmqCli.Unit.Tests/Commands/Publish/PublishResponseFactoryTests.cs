using RabbitMQ.Client;
using RmqCli.Commands.Publish;
using RmqCli.Core.Models;

namespace RmqCli.Unit.Tests.Commands.Publish;

public class PublishResponseFactoryTests
{
    public class Success
    {
        [Fact]
        public void SetsStatus_ToSuccess()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = CreatePublishResults(count: 1);
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Status.Should().Be("success");
        }

        [Fact]
        public void SetsMessagesFailed_ToZero()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = CreatePublishResults(count: 1);
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Result.Should().NotBeNull();
            response.Result!.MessagesFailed.Should().Be(0);
        }

        [Fact]
        public void SetsMessagesPublished_ToResultCount()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = CreatePublishResults(count: 5);
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Result!.MessagesPublished.Should().Be(5);
        }

        [Fact]
        public void SetsDestination_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("my-queue");
            var results = CreatePublishResults(count: 1);
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Destination.Should().NotBeNull();
            response.Destination!.Queue.Should().Be("my-queue");
        }

        [Fact]
        public void CalculatesAverageMessageSize_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = new List<PublishOperationDto>
            {
                CreatePublishOperation("msg-1", messageLength: 1024),
                CreatePublishOperation("msg-2", messageLength: 2048),
                CreatePublishOperation("msg-3", messageLength: 3072)
            };
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            // Average: (1024 + 2048 + 3072) / 3 = 2048
            response.Result!.AverageMessageSizeBytes.Should().Be(2048);
            response.Result!.AverageMessageSize.Should().Be("2 KB");
        }

        [Fact]
        public void CalculatesTotalSize_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = new List<PublishOperationDto>
            {
                CreatePublishOperation("msg-1", messageLength: 1024),
                CreatePublishOperation("msg-2", messageLength: 2048)
            };
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Result!.TotalSizeBytes.Should().Be(3072);
            response.Result!.TotalSize.Should().Be("3 KB");
        }

        [Fact]
        public void CalculatesMessagesPerSecond_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = CreatePublishResults(count: 10);
            var duration = TimeSpan.FromSeconds(2);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Result!.MessagesPerSecond.Should().Be(5.0);
        }

        [Fact]
        public void SetsDuration_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = CreatePublishResults(count: 1);
            var duration = TimeSpan.FromMilliseconds(1500);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Result!.DurationMs.Should().Be(1500);
            response.Result!.Duration.Should().Be("1s 500ms");
        }

        [Fact]
        public void SetsMessageIds_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = new List<PublishOperationDto>
            {
                CreatePublishOperation("msg-001"),
                CreatePublishOperation("msg-002"),
                CreatePublishOperation("msg-003")
            };
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Result!.MessageIds.Should().HaveCount(3);
            response.Result!.MessageIds.Should().ContainInOrder("msg-001", "msg-002", "msg-003");
        }

        [Fact]
        public void SetsFirstAndLastMessageId_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = new List<PublishOperationDto>
            {
                CreatePublishOperation("first-msg"),
                CreatePublishOperation("middle-msg"),
                CreatePublishOperation("last-msg")
            };
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Result!.FirstMessageId.Should().Be("first-msg");
            response.Result!.LastMessageId.Should().Be("last-msg");
        }

        [Fact]
        public void SetsTimestamps_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var timestamp1 = new AmqpTimestamp(1609459200); // 2021-01-01 00:00:00
            var timestamp2 = new AmqpTimestamp(1609545600); // 2021-01-02 00:00:00
            var results = new List<PublishOperationDto>
            {
                new PublishOperationDto("msg-1", 100, timestamp1),
                new PublishOperationDto("msg-2", 100, timestamp2)
            };
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Result!.FirstTimestamp.Should().Be("2021-01-01 00:00:00");
            response.Result!.LastTimestamp.Should().Be("2021-01-02 00:00:00");
        }

        [Fact]
        public void HandlesSingleMessage_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = new List<PublishOperationDto>
            {
                CreatePublishOperation("only-msg", messageLength: 512)
            };
            var duration = TimeSpan.FromMilliseconds(100);

            // Act
            var response = PublishResponseFactory.Success(destination, results, duration);

            // Assert
            response.Status.Should().Be("success");
            response.Result!.MessagesPublished.Should().Be(1);
            response.Result!.MessagesFailed.Should().Be(0);
            response.Result!.FirstMessageId.Should().Be("only-msg");
            response.Result!.LastMessageId.Should().Be("only-msg");
            response.Result!.TotalSizeBytes.Should().Be(512);
            response.Result!.AverageMessageSizeBytes.Should().Be(512);
        }
    }

    public class Partial
    {
        [Fact]
        public void SetsStatus_ToPartial()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = CreatePublishResults(count: 3);
            var failedMessages = 2;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Partial(destination, results, failedMessages, duration);

            // Assert
            response.Status.Should().Be("partial");
        }

        [Fact]
        public void SetsMessagesFailed_ToProvidedValue()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = CreatePublishResults(count: 3);
            var failedMessages = 2;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Partial(destination, results, failedMessages, duration);

            // Assert
            response.Result!.MessagesFailed.Should().Be(2);
        }

        [Fact]
        public void SetsMessagesPublished_ToResultCount()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = CreatePublishResults(count: 3);
            var failedMessages = 2;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Partial(destination, results, failedMessages, duration);

            // Assert
            response.Result!.MessagesPublished.Should().Be(3);
        }

        [Fact]
        public void IncludesAllSuccessfulMessages_InMessageIds()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = new List<PublishOperationDto>
            {
                CreatePublishOperation("msg-001"),
                CreatePublishOperation("msg-002"),
                CreatePublishOperation("msg-003")
            };
            var failedMessages = 2;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Partial(destination, results, failedMessages, duration);

            // Assert
            response.Result!.MessageIds.Should().HaveCount(3);
            response.Result!.MessageIds.Should().ContainInOrder("msg-001", "msg-002", "msg-003");
        }

        [Fact]
        public void CalculatesStatistics_BasedOnSuccessfulMessages()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var results = new List<PublishOperationDto>
            {
                CreatePublishOperation("msg-1", messageLength: 1000),
                CreatePublishOperation("msg-2", messageLength: 2000)
            };
            var failedMessages = 3;
            var duration = TimeSpan.FromSeconds(2);

            // Act
            var response = PublishResponseFactory.Partial(destination, results, failedMessages, duration);

            // Assert
            response.Result!.TotalSizeBytes.Should().Be(3000);
            response.Result!.AverageMessageSizeBytes.Should().Be(1500);
            response.Result!.MessagesPerSecond.Should().Be(1.0);
        }
    }

    public class Failure
    {
        [Fact]
        public void SetsStatus_ToFailure()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var totalMessages = 5;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Failure(destination, totalMessages, duration);

            // Assert
            response.Status.Should().Be("failure");
        }

        [Fact]
        public void SetsMessagesPublished_ToZero()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var totalMessages = 5;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Failure(destination, totalMessages, duration);

            // Assert
            response.Result!.MessagesPublished.Should().Be(0);
        }

        [Fact]
        public void SetsMessagesFailed_ToTotalMessages()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var totalMessages = 10;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Failure(destination, totalMessages, duration);

            // Assert
            response.Result!.MessagesFailed.Should().Be(10);
        }

        [Fact]
        public void SetsSizes_ToZero()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var totalMessages = 5;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Failure(destination, totalMessages, duration);

            // Assert
            response.Result!.TotalSize.Should().Be("0 B");
            response.Result!.AverageMessageSize.Should().Be("0 B");
        }

        [Fact]
        public void SetsDuration_Correctly()
        {
            // Arrange
            var destination = CreateQueueDestination("test-queue");
            var totalMessages = 5;
            var duration = TimeSpan.FromMilliseconds(2500);

            // Act
            var response = PublishResponseFactory.Failure(destination, totalMessages, duration);

            // Assert
            response.Result!.DurationMs.Should().Be(2500);
            response.Result!.Duration.Should().Be("2s 500ms");
        }

        [Fact]
        public void SetsDestination_Correctly()
        {
            // Arrange
            var destination = CreateExchangeDestination("my-exchange", "routing.key");
            var totalMessages = 5;
            var duration = TimeSpan.FromSeconds(1);

            // Act
            var response = PublishResponseFactory.Failure(destination, totalMessages, duration);

            // Assert
            response.Destination.Should().NotBeNull();
            response.Destination!.Exchange.Should().Be("my-exchange");
            response.Destination!.RoutingKey.Should().Be("routing.key");
        }
    }

    #region Test Helpers

    private static DestinationInfo CreateQueueDestination(string queueName)
    {
        return new DestinationInfo
        {
            Type = "queue",
            Queue = queueName
        };
    }

    private static DestinationInfo CreateExchangeDestination(string exchangeName, string routingKey)
    {
        return new DestinationInfo
        {
            Type = "exchange",
            Exchange = exchangeName,
            RoutingKey = routingKey
        };
    }

    private static List<PublishOperationDto> CreatePublishResults(int count, long messageLength = 100)
    {
        var results = new List<PublishOperationDto>();
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        for (int i = 0; i < count; i++)
        {
            results.Add(CreatePublishOperation($"msg-{i + 1:D3}", messageLength, baseTimestamp + i));
        }

        return results;
    }

    private static PublishOperationDto CreatePublishOperation(
        string messageId,
        long messageLength = 100,
        long? unixTimestamp = null)
    {
        var timestamp = new AmqpTimestamp(unixTimestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return new PublishOperationDto(messageId, messageLength, timestamp);
    }

    #endregion
}