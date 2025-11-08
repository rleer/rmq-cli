using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Shared;
using RmqCli.Shared.Output;
using RmqCli.Shared.Output.Formatters;
using Xunit.Abstractions;

namespace RmqCli.Integration.Tests.Infrastructure.Output;

public class ConsoleOutputTests
{
    #region WriteMessagesAsync

    public class WriteMessagesAsync : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly TextWriter _originalOut;

        public WriteMessagesAsync(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            // Redirect console output to suppress test noise
            _originalOut = Console.Out;
            Console.SetOut(TextWriter.Null);
        }

        public void Dispose()
        {
            // Restore original console output
            Console.SetOut(_originalOut);
        }

        [Fact]
        public async Task ProcessesMessages_WithPlainFormat()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            // Add test messages
            var message1 = CreateRetrievedMessage("Test message 1", "exchange", "routing-key", "test-queue", 1);
            var message2 = CreateRetrievedMessage("Test message 2", "exchange", "routing-key", "test-queue", 2);

            await messageChannel.Writer.WriteAsync(message1);
            await messageChannel.Writer.WriteAsync(message2);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ProcessedCount.Should().Be(2);
            result.TotalBytes.Should().BeGreaterThan(0);

            // Verify acks were sent
            ackChannel.Reader.TryRead(out var ack1).Should().BeTrue();
            ack1.Item1.Should().Be(1);
            ack1.Item2.Should().Be(true);

            ackChannel.Reader.TryRead(out var ack2).Should().BeTrue();
            ack2.Item1.Should().Be(2);
            ack2.Item2.Should().Be(true);
        }

        [Fact]
        public async Task ProcessesMessages_WithJsonFormat()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = OutputFormat.Json, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var message = CreateRetrievedMessage("Test message", "exchange", "routing-key", "test-queue", 1);
            await messageChannel.Writer.WriteAsync(message);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ProcessedCount.Should().Be(1);
            result.TotalBytes.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task HandlesEmptyMessageChannel()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            messageChannel.Writer.Complete(); // No messages

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ProcessedCount.Should().Be(0);
            result.TotalBytes.Should().Be(0);
        }

        [Fact]
        public async Task StopsProcessingAfterCancellation()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            // Use bounded channel to control message flow rate
            var messageChannel = Channel.CreateBounded<RetrievedMessage>(500);
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            const int totalMessages = 100000;
            using var cts = new CancellationTokenSource();

            // Start processing in background BEFORE all messages are added
            var processingTask = output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                cts.Token);

            // Writer task that adds messages concurrently
            var writerTask = Task.Run(async () =>
            {
                for (int i = 1; i <= totalMessages; i++)
                {
                    // Stop writing if cancelled
                    if (cts.Token.IsCancellationRequested)
                        break;

                    await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));

                    // Cancel after some messages have been written
                    if (i == 1000)
                    {
                        cts.Cancel();
                    }
                }

                messageChannel.Writer.Complete();
            });

            // Wait for processing to complete
            var result = await processingTask;
            await writerTask; // Ensure writer completes

            // Assert - Should have processed some messages but not all
            result.Should().NotBeNull();
            result.ProcessedCount.Should().BeGreaterThan(100, "at least 100 messages should be processed before cancellation");
            result.ProcessedCount.Should().BeLessThan(totalMessages, "cancellation should stop processing before all messages are consumed");

            _testOutputHelper.WriteLine($"Processed {result.ProcessedCount} out of {totalMessages} messages before cancellation took effect");
        }

        [Fact]
        public async Task SendsAcknowledgments_WithCorrectMode()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var message = CreateRetrievedMessage("Test", "exchange", "routing-key", "test-queue", 42);
            await messageChannel.Writer.WriteAsync(message);
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            ackChannel.Reader.TryRead(out var ack).Should().BeTrue();
            ack.Item1.Should().Be(42);
            ack.Item2.Should().Be(true);
        }

        [Fact]
        public async Task CalculatesTotalBytes_Correctly()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var body1 = "Message 1";
            var body2 = "Message 2 - longer";
            var expectedBytes = System.Text.Encoding.UTF8.GetByteCount(body1)
                                + System.Text.Encoding.UTF8.GetByteCount(body2);

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage(body1, "exchange", "routing-key", "test-queue", 1));
            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage(body2, "exchange", "routing-key", "test-queue", 2));
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.TotalBytes.Should().Be(expectedBytes);
        }

        [Fact]
        public async Task HandlesMessagesWithProperties()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");

            var message = CreateRetrievedMessage("Test with properties", "exchange", "routing-key", "test-queue", 1, props);
            await messageChannel.Writer.WriteAsync(message);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(1);
        }

        [Fact]
        public async Task HandlesRedeliveredMessages()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var message = CreateRetrievedMessage("Redelivered message", "exchange", "routing-key", "test-queue", 1, redelivered: true);
            await messageChannel.Writer.WriteAsync(message);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(1);
        }
    }

    #endregion

    #region Test Helpers

    private static RetrievedMessage CreateRetrievedMessage(
        string body,
        string exchange = "exchange",
        string routingKey = "routing.key",
        string queue = "test-queue",
        ulong deliveryTag = 1,
        IReadOnlyBasicProperties? props = null,
        bool redelivered = false)
    {
        var (properties, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);
        var bodySizeBytes = Encoding.UTF8.GetByteCount(body);

        return new RetrievedMessage
        {
            Body = body,
            Exchange = exchange,
            RoutingKey = routingKey,
            Queue = queue,
            DeliveryTag = deliveryTag,
            Properties = properties,
            Headers = headers,
            Redelivered = redelivered,
            BodySizeBytes = bodySizeBytes,
            BodySize = OutputUtilities.ToSizeString(bodySizeBytes)
        };
    }

    #endregion
}