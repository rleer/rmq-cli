using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Shared.Output;
using RmqCli.Shared.Output.Formatters;
using Xunit.Abstractions;

namespace RmqCli.Integration.Tests.Infrastructure.Output;

[Collection("ConsoleOutputTests")]
public class ConsoleOutputTests
{
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

        [Theory]
        [InlineData(OutputFormat.Plain)]
        [InlineData(OutputFormat.Json)]
        [InlineData(OutputFormat.Table)]
        public async Task ProcessesMessages_WithAllOutputFormats(OutputFormat format)
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = format, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            // Add test messages
            var message1 = CreateRetrievedMessage("Test message 1", deliveryTag: 1);
            var message2 = CreateRetrievedMessage("Test message 2", deliveryTag: 2);

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

                    await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", deliveryTag: (ulong)i));

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
            var expectedBytes = Encoding.UTF8.GetByteCount(body1) + Encoding.UTF8.GetByteCount(body2);

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage(body1, deliveryTag: 1));
            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage(body2, deliveryTag: 2));
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
        public async Task HandlesWriteError_SendsNack()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            // Use an unsupported format to trigger error
            var output = new ConsoleOutput(logger,
                new OutputOptions { Format = (OutputFormat)3, OutputFile = null, Compact = false, Quiet = false, Verbose = false, NoColor = false });

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage("Message 1", deliveryTag: 1));
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            // Should receive a NACK (false)
            ackChannel.Reader.TryRead(out var ack).Should().BeTrue();
            ack.Item1.Should().Be(1);
            ack.Item2.Should().BeFalse();
            result.ProcessedCount.Should().Be(0);
            result.TotalBytes.Should().Be(0);
        }
    }

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