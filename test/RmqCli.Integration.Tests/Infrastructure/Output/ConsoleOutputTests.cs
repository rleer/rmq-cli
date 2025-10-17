using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Commands.Consume;
using RmqCli.Infrastructure.Output.Console;
using RmqCli.Shared;
using Xunit.Abstractions;

namespace RmqCli.Integration.Tests.Infrastructure.Output;

public class ConsoleOutputTests
{
    #region WriteMessagesAsync

    public class WriteMessagesAsync
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public WriteMessagesAsync(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task ProcessesMessages_WithPlainFormat()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger, OutputFormat.Plain);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            // Add test messages
            var message1 = new RabbitMessage("Test message 1", 1, null, false);
            var message2 = new RabbitMessage("Test message 2", 2, null, false);

            await messageChannel.Writer.WriteAsync(message1);
            await messageChannel.Writer.WriteAsync(message2);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                AckModes.Ack,
                CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ProcessedCount.Should().Be(2);
            result.TotalBytes.Should().BeGreaterThan(0);

            // Verify acks were sent
            ackChannel.Reader.TryRead(out var ack1).Should().BeTrue();
            ack1.Item1.Should().Be(1);
            ack1.Item2.Should().Be(AckModes.Ack);

            ackChannel.Reader.TryRead(out var ack2).Should().BeTrue();
            ack2.Item1.Should().Be(2);
            ack2.Item2.Should().Be(AckModes.Ack);
        }

        [Fact]
        public async Task ProcessesMessages_WithJsonFormat()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger, OutputFormat.Json);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            var message = new RabbitMessage("Test message", 1, null, false);
            await messageChannel.Writer.WriteAsync(message);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                AckModes.Ack,
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
            var output = new ConsoleOutput(logger, OutputFormat.Plain);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            messageChannel.Writer.Complete(); // No messages

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                AckModes.Ack,
                CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ProcessedCount.Should().Be(0);
            result.TotalBytes.Should().Be(0);
        }

        [Fact]
        public async Task RespectsCancellationToken()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger, OutputFormat.Plain);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            // Add multiple messages
            for (int i = 1; i <= 1500; i++)
            {
                await messageChannel.Writer.WriteAsync(new RabbitMessage($"Message {i}", (ulong)i, null, false));
            }
            messageChannel.Writer.Complete();

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(5)); // Cancel after ~500 messages

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                AckModes.Ack,
                cts.Token);

            // Assert - Should process at least 100 message but not all 1500
            result.ProcessedCount.Should().BeGreaterThan(100);
            result.ProcessedCount.Should().BeLessThan(1500);
            _testOutputHelper.WriteLine($"processed: {result.ProcessedCount}");
        }

        [Fact]
        public async Task SendsAcknowledgments_WithCorrectMode()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger, OutputFormat.Plain);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            var message = new RabbitMessage("Test", 42, null, false);
            await messageChannel.Writer.WriteAsync(message);
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                AckModes.Reject, // Use Reject mode
                CancellationToken.None);

            // Assert
            ackChannel.Reader.TryRead(out var ack).Should().BeTrue();
            ack.Item1.Should().Be(42);
            ack.Item2.Should().Be(AckModes.Reject);
        }

        [Fact]
        public async Task CalculatesTotalBytes_Correctly()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger, OutputFormat.Plain);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            var body1 = "Message 1";
            var body2 = "Message 2 - longer";
            var expectedBytes = System.Text.Encoding.UTF8.GetByteCount(body1)
                              + System.Text.Encoding.UTF8.GetByteCount(body2);

            await messageChannel.Writer.WriteAsync(new RabbitMessage(body1, 1, null, false));
            await messageChannel.Writer.WriteAsync(new RabbitMessage(body2, 2, null, false));
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                AckModes.Ack,
                CancellationToken.None);

            // Assert
            result.TotalBytes.Should().Be(expectedBytes);
        }

        [Fact]
        public async Task HandlesMessagesWithProperties()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger, OutputFormat.Plain);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");

            var message = new RabbitMessage("Test with properties", 1, props, false);
            await messageChannel.Writer.WriteAsync(message);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                AckModes.Ack,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(1);
        }

        [Fact]
        public async Task HandlesRedeliveredMessages()
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger, OutputFormat.Plain);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            var message = new RabbitMessage("Redelivered message", 1, null, Redelivered: true);
            await messageChannel.Writer.WriteAsync(message);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                AckModes.Ack,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(1);
        }

        [Theory]
        [InlineData(AckModes.Ack)]
        [InlineData(AckModes.Reject)]
        [InlineData(AckModes.Requeue)]
        public async Task HandlesAllAckModes(AckModes ackMode)
        {
            // Arrange
            var logger = new NullLogger<ConsoleOutput>();
            var output = new ConsoleOutput(logger, OutputFormat.Plain);

            var messageChannel = Channel.CreateUnbounded<RabbitMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, AckModes)>();

            await messageChannel.Writer.WriteAsync(new RabbitMessage("Test", 1, null, false));
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                ackMode,
                CancellationToken.None);

            // Assert
            ackChannel.Reader.TryRead(out var ack).Should().BeTrue();
            ack.Item2.Should().Be(ackMode);
        }
    }

    #endregion
}
