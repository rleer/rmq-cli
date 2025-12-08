using RmqCli.Commands.MessageRetrieval;
using RmqCli.Shared.Json;
using RmqCli.Shared.Output;

namespace RmqCli.Integration.Tests.Commands.MessageRetrieval;

[Collection("ConsoleOutputTests")]
public class MessageRetrievalResultOutputServiceTests : IDisposable
{
    private readonly StringWriter _stringWriter;
    private readonly TextWriter _originalError;

    public MessageRetrievalResultOutputServiceTests()
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
    public void WriteMessageRetrievalResult_PlainOrTableFormat_WritesExpectedOutput(OutputFormat format)
    {
        // Arrange
        var options = new OutputOptions { Format = format, NoColor = true };
        var service = new MessageRetrievalResultOutputService(options);
        var response = new MessageRetrievalResponse
        {
            Queue = "test-queue",
            Result = new MessageRetrievalResult
            {
                RetrievalMode = "subscribe",
                AckMode = "ack",
                MessagesReceived = 10,
                MessagesProcessed = 10,
                TotalSize = "1.5 KB"
            }
        };

        // Act
        service.WriteMessageRetrievalResult(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().Contain("  Queue:      test-queue");
        output.Should().Contain("  Mode:       subscribe");
        output.Should().Contain("  Ack Mode:   ack");
        output.Should().Contain("  Received:   10 messages");
        output.Should().Contain("  Processed:  10 messages");
        output.Should().Contain("  Total size: 1.5 KB");
    }

    [Fact]
    public void WriteMessageRetrievalResult_JsonFormat_WritesJson()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Json };
        var service = new MessageRetrievalResultOutputService(options);
        var timestamp = DateTime.Now;
        var response = new MessageRetrievalResponse
        {
            Status = "success",
            Timestamp = timestamp,
            Queue = "test-queue",
            Result = new MessageRetrievalResult
            {
                RetrievalMode = "subscribe",
                AckMode = "ack",
                MessagesReceived = 5,
                MessagesProcessed = 5,
                DurationMs = 100,
                Duration = "100ms",
                CancellationReason = null,
                MessagesPerSecond = 0.02,
                TotalSizeBytes = 500,
                TotalSize = "500 B"
            }
        };

        // Act
        service.WriteMessageRetrievalResult(response);

        // Assert
        Console.Error.Flush();
        var output = _stringWriter.ToString();
        output.Should().NotBeNullOrWhiteSpace();
        var deserialized = System.Text.Json.JsonSerializer.Deserialize(output, JsonSerializationContext.RelaxedEscaping.MessageRetrievalResponse);
        deserialized.Should().NotBeNull();
        deserialized.Queue.Should().Be("test-queue");
        deserialized.Status.Should().Be("success");
        deserialized.Timestamp.Should().Be(timestamp);
        deserialized.Result.Should().NotBeNull();
        deserialized.Result.RetrievalMode.Should().Be("subscribe");
        deserialized.Result.AckMode.Should().Be("ack");
        deserialized.Result.MessagesReceived.Should().Be(5);
        deserialized.Result.MessagesProcessed.Should().Be(5);
        deserialized.Result.DurationMs.Should().Be(100);
        deserialized.Result.Duration.Should().Be("100ms");
        deserialized.Result.CancellationReason.Should().BeNull();
        deserialized.Result.MessagesPerSecond.Should().Be(0.02);
        deserialized.Result.TotalSizeBytes.Should().Be(500);
        deserialized.Result.TotalSize.Should().Be("500 B");
        
    }

    [Fact]
    public void WriteMessageRetrievalResult_QuietMode_WritesNothing()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Plain, Quiet = true };
        var service = new MessageRetrievalResultOutputService(options);
        var response = new MessageRetrievalResponse
        {
            Queue = "test-queue",
            Result = new MessageRetrievalResult()
        };

        // Act
        service.WriteMessageRetrievalResult(response);

        // Assert
        _stringWriter.ToString().Should().BeEmpty();
    }

    [Fact]
    public void WriteMessageRetrievalResult_WithSkippedMessages_ShowsSkippedCount()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Plain, NoColor = true };
        var service = new MessageRetrievalResultOutputService(options);
        var response = new MessageRetrievalResponse
        {
            Queue = "test-queue",
            Result = new MessageRetrievalResult
            {
                RetrievalMode = "subscribe",
                AckMode = "ack",
                MessagesReceived = 10,
                MessagesProcessed = 8, // 2 skipped
                TotalSize = "1.0 KB"
            }
        };

        // Act
        service.WriteMessageRetrievalResult(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().Contain("Processed:  8 messages (2 skipped & requeued by RabbitMQ)");
    }
}
