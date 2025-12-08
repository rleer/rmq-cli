using RmqCli.Commands.Publish;
using RmqCli.Core.Models;
using RmqCli.Shared.Json;
using RmqCli.Shared.Output;

namespace RmqCli.Integration.Tests.Commands.Publish;

[Collection("ConsoleOutputTests")]
public class PublishOutputServiceTests : IDisposable
{
    private readonly StringWriter _stringWriter;
    private readonly TextWriter _originalError;

    public PublishOutputServiceTests()
    {
        _stringWriter = new StringWriter();
        _originalError = Console.Error;
        Console.SetError(_stringWriter);
    }

    public void Dispose()
    {
        Console.SetError(_originalError);
        _stringWriter.Dispose();
    }

    [Theory]
    [InlineData(OutputFormat.Plain)]
    [InlineData(OutputFormat.Table)]
    public void WritePublishResult_NonJsonFormat_SingleMessageToQueue_WritesExpectedOutput(OutputFormat format)
    {
        // Arrange
        var options = new OutputOptions { Format = format, NoColor = true };
        var service = new PublishOutputService(options);
        var response = new PublishResponse
        {
            Destination = new DestinationInfo { Queue = "test-queue" },
            Result = new PublishResult
            {
                MessagesPublished = 1,
                FirstMessageId = "msg-123",
                TotalSize = "1.0 KB",
                FirstTimestamp = "2024-01-01T12:00:00"
            }
        };

        // Act
        service.WritePublishResult(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().Contain("  Queue:       test-queue");
        output.Should().Contain("  Message ID:  msg-123");
        output.Should().Contain("  Size:        1.0 KB");
        output.Should().Contain("  Timestamp:   2024-01-01T12:00:00 UTC");
    }

    [Theory]
    [InlineData(OutputFormat.Plain)]
    [InlineData(OutputFormat.Table)]
    public void WritePublishResult_NonJsonFormat_MultipleMessagesToQueue_WritesExpectedOutput(OutputFormat format)
    {
        // Arrange
        var options = new OutputOptions { Format = format, NoColor = true };
        var service = new PublishOutputService(options);
        var response = new PublishResponse
        {
            Destination = new DestinationInfo { Queue = "test-queue" },
            Result = new PublishResult
            {
                MessagesPublished = 10,
                FirstMessageId = "msg-001",
                LastMessageId = "msg-010",
                AverageMessageSize = "512 B",
                TotalSize = "5.0 KB",
                FirstTimestamp = "2024-01-01T12:00:00",
                LastTimestamp = "2024-01-01T12:00:05"
            }
        };

        // Act
        service.WritePublishResult(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().Contain("  Queue:       test-queue");
        output.Should().Contain("  Message IDs: msg-001 → msg-010");
        output.Should().Contain("  Size:        512 B avg. (5.0 KB total)");
        output.Should().Contain("  Time:        2024-01-01T12:00:00 UTC → 2024-01-01T12:00:05 UTC");
    }

    [Theory]
    [InlineData(OutputFormat.Plain)]
    [InlineData(OutputFormat.Table)]
    public void WritePublishResult_NonJsonFormat_SingleMessageToExchange_WritesExpectedOutput(OutputFormat format)
    {
        // Arrange
        var options = new OutputOptions { Format = format, NoColor = true };
        var service = new PublishOutputService(options);
        var response = new PublishResponse
        {
            Destination = new DestinationInfo
            {
                Exchange = "test-exchange",
                RoutingKey = "test.routing.key"
            },
            Result = new PublishResult
            {
                MessagesPublished = 1,
                FirstMessageId = "msg-456",
                TotalSize = "2.5 KB",
                FirstTimestamp = "2024-01-02T10:30:00"
            }
        };

        // Act
        service.WritePublishResult(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().Contain("  Exchange:    test-exchange");
        output.Should().Contain("  Routing Key: test.routing.key");
        output.Should().Contain("  Message ID:  msg-456");
        output.Should().Contain("  Size:        2.5 KB");
        output.Should().Contain("  Timestamp:   2024-01-02T10:30:00 UTC");
    }

    [Theory]
    [InlineData(OutputFormat.Plain)]
    [InlineData(OutputFormat.Table)]
    public void WritePublishResult_NonJsonFormat_MultipleMessagesToExchange_WritesExpectedOutput(OutputFormat format)
    {
        // Arrange
        var options = new OutputOptions { Format = format, NoColor = true };
        var service = new PublishOutputService(options);
        var response = new PublishResponse
        {
            Destination = new DestinationInfo
            {
                Exchange = "amq.topic",
                RoutingKey = "events.user.created"
            },
            Result = new PublishResult
            {
                MessagesPublished = 5,
                FirstMessageId = "msg-100",
                LastMessageId = "msg-104",
                AverageMessageSize = "1.2 KB",
                TotalSize = "6.0 KB",
                FirstTimestamp = "2024-01-03T08:00:00",
                LastTimestamp = "2024-01-03T08:00:10"
            }
        };

        // Act
        service.WritePublishResult(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().Contain("  Exchange:    amq.topic");
        output.Should().Contain("  Routing Key: events.user.created");
        output.Should().Contain("  Message IDs: msg-100 → msg-104");
        output.Should().Contain("  Size:        1.2 KB avg. (6.0 KB total)");
        output.Should().Contain("  Time:        2024-01-03T08:00:00 UTC → 2024-01-03T08:00:10 UTC");
    }

    [Fact]
    public void WritePublishResult_JsonFormat_WritesJson()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Json };
        var service = new PublishOutputService(options);
        var timestamp = DateTime.Now;
        var response = new PublishResponse
        {
            Status = "success",
            Timestamp = timestamp,
            Destination = new DestinationInfo { Queue = "test-queue" },
            Result = new PublishResult
            {
                MessagesPublished = 3,
                MessagesFailed = 0,
                DurationMs = 150.5,
                Duration = "150ms",
                MessageIds = new List<string> { "msg-001", "msg-002", "msg-003" },
                FirstMessageId = "msg-001",
                LastMessageId = "msg-003",
                FirstTimestamp = "2024-01-01T12:00:00",
                LastTimestamp = "2024-01-01T12:00:01",
                AverageMessageSizeBytes = 1024.0,
                AverageMessageSize = "1.0 KB",
                TotalSizeBytes = 3072,
                TotalSize = "3.0 KB",
                MessagesPerSecond = 20.0
            }
        };

        // Clear any buffered content before Act to prevent test pollution
        _stringWriter.GetStringBuilder().Clear();

        // Act
        service.WritePublishResult(response);

        // Assert
        Console.Error.Flush();
        var output = _stringWriter.ToString();
        output.Should().NotBeNullOrWhiteSpace();
        var deserialized = System.Text.Json.JsonSerializer.Deserialize(output, JsonSerializationContext.RelaxedEscaping.PublishResponse);
        deserialized.Should().NotBeNull();
        deserialized.Status.Should().Be("success");
        deserialized.Timestamp.Should().Be(timestamp);
        deserialized.Destination.Should().NotBeNull();
        deserialized.Destination!.Queue.Should().Be("test-queue");
        deserialized.Result.Should().NotBeNull();
        deserialized.Result!.MessagesPublished.Should().Be(3);
        deserialized.Result.MessagesFailed.Should().Be(0);
        deserialized.Result.DurationMs.Should().Be(150.5);
        deserialized.Result.Duration.Should().Be("150ms");
        deserialized.Result.MessageIds.Should().HaveCount(3);
        deserialized.Result.MessageIds.Should().ContainInOrder("msg-001", "msg-002", "msg-003");
        deserialized.Result.FirstMessageId.Should().Be("msg-001");
        deserialized.Result.LastMessageId.Should().Be("msg-003");
        deserialized.Result.FirstTimestamp.Should().Be("2024-01-01T12:00:00");
        deserialized.Result.LastTimestamp.Should().Be("2024-01-01T12:00:01");
        deserialized.Result.AverageMessageSizeBytes.Should().Be(1024.0);
        deserialized.Result.AverageMessageSize.Should().Be("1.0 KB");
        deserialized.Result.TotalSizeBytes.Should().Be(3072);
        deserialized.Result.TotalSize.Should().Be("3.0 KB");
        deserialized.Result.MessagesPerSecond.Should().Be(20.0);
    }

    [Fact]
    public void WritePublishResult_JsonFormat_WithExchange_WritesJson()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Json };
        var service = new PublishOutputService(options);
        var timestamp = DateTime.Now;
        var response = new PublishResponse
        {
            Status = "success",
            Timestamp = timestamp,
            Destination = new DestinationInfo
            {
                Exchange = "test-exchange",
                RoutingKey = "test.key"
            },
            Result = new PublishResult
            {
                MessagesPublished = 5,
                MessagesFailed = 0,
                DurationMs = 100,
                Duration = "100ms",
                MessageIds = ["msg-001", "msg-002", "msg-003", "msg-004", "msg-005"],
                FirstMessageId = "msg-001",
                LastMessageId = "msg-005",
                FirstTimestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                LastTimestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                AverageMessageSize = "100 B",
                AverageMessageSizeBytes = 100.0,
                TotalSizeBytes = 500,
                TotalSize = "500 B",
                MessagesPerSecond = 0.02
            }
        };

        // Act
        service.WritePublishResult(response);

        // Assert
        Console.Error.Flush();
        var output = _stringWriter.ToString();
        output.Should().NotBeNullOrWhiteSpace();

        var deserialized = System.Text.Json.JsonSerializer.Deserialize(output, JsonSerializationContext.RelaxedEscaping.PublishResponse);

        deserialized.Should().NotBeNull();
        deserialized.Status.Should().Be("success");
        deserialized.Timestamp.Should().Be(timestamp);

        deserialized.Destination.Should().NotBeNull();
        deserialized.Destination.Exchange.Should().Be("test-exchange");
        deserialized.Destination.RoutingKey.Should().Be("test.key");

        deserialized.Result.Should().NotBeNull();
        deserialized.Result!.MessagesPublished.Should().Be(5);
        deserialized.Result.MessagesFailed.Should().Be(0);
        deserialized.Result.DurationMs.Should().Be(100);
        deserialized.Result.Duration.Should().Be("100ms");
        deserialized.Result.MessageIds.Should().HaveCount(5);
        deserialized.Result.MessageIds.Should().ContainInOrder("msg-001", "msg-002", "msg-003", "msg-004", "msg-005");
        deserialized.Result.FirstMessageId.Should().Be("msg-001");
        deserialized.Result.LastMessageId.Should().Be("msg-005");
        deserialized.Result.FirstTimestamp.Should().Be(timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
        deserialized.Result.LastTimestamp.Should().Be(timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
        deserialized.Result.AverageMessageSizeBytes.Should().Be(100.0);
        deserialized.Result.AverageMessageSize.Should().Be("100 B");
        deserialized.Result.TotalSizeBytes.Should().Be(500);
        deserialized.Result.TotalSize.Should().Be("500 B");
        deserialized.Result.MessagesPerSecond.Should().Be(0.02);
    }

    [Fact]
    public void WritePublishResult_QuietMode_WritesNothing()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Plain, Quiet = true };
        var service = new PublishOutputService(options);
        var response = new PublishResponse
        {
            Destination = new DestinationInfo { Queue = "test-queue" },
            Result = new PublishResult
            {
                MessagesPublished = 1,
                FirstMessageId = "msg-123"
            }
        };

        // Act
        service.WritePublishResult(response);

        // Assert
        _stringWriter.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(OutputFormat.Plain)]
    [InlineData(OutputFormat.Table)]
    public void WritePublishResult_NonJsonFormat_NoDestination_WritesResultOnly(OutputFormat format)
    {
        // Arrange
        var options = new OutputOptions { Format = format, NoColor = true };
        var service = new PublishOutputService(options);
        var response = new PublishResponse
        {
            Result = new PublishResult
            {
                MessagesPublished = 1,
                FirstMessageId = "msg-999",
                TotalSize = "100 B",
                FirstTimestamp = "2024-01-01T00:00:00"
            }
        };

        // Act
        service.WritePublishResult(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().NotContain("Queue:");
        output.Should().NotContain("Exchange:");
        output.Should().Contain("  Message ID:  msg-999");
        output.Should().Contain("  Size:        100 B");
        output.Should().Contain("  Timestamp:   2024-01-01T00:00:00 UTC");
    }

    [Theory]
    [InlineData(OutputFormat.Plain)]
    [InlineData(OutputFormat.Table)]
    public void WritePublishResult_PlainFormat_NoResult_WritesDestinationOnly(OutputFormat format)
    {
        // Arrange
        var options = new OutputOptions { Format = format, NoColor = true };
        var service = new PublishOutputService(options);
        var response = new PublishResponse
        {
            Destination = new DestinationInfo { Queue = "test-queue" }
        };

        // Act
        service.WritePublishResult(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().Contain("  Queue:       test-queue");
        output.Should().NotContain("Message ID:");
        output.Should().NotContain("Size:");
        output.Should().NotContain("Timestamp:");
    }
}