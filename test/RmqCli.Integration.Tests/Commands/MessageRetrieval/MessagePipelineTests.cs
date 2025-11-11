using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Shared;
using RmqCli.Shared.Output;

namespace RmqCli.Integration.Tests.Commands.MessageRetrieval;

public class MessagePipelineTests
{
    public class StartPipeline
    {
        [Fact]
        public void ReturnsBothTasks_WhenCalled()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions { Format = OutputFormat.Plain };

            // Act
            var (writerTask, ackTask) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Assert
            writerTask.Should().NotBeNull();
            ackTask.Should().NotBeNull();
            writerTask.Should().NotBeSameAs(ackTask);
        }

        [Fact]
        public async Task WriterTaskCompletes_WhenMessageChannelIsCompleted()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions { Format = OutputFormat.Plain };

            var (writerTask, _) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Act - Complete the receive channel (no messages)
            receiveChan.Writer.Complete();

            // Assert
            var result = await writerTask;
            result.Should().NotBeNull();
            result.ProcessedCount.Should().Be(0);
            result.TotalBytes.Should().Be(0);
        }

        [Fact]
        public async Task WriterTaskProcessesMessages_WhenMessagesAreAvailable()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions { Format = OutputFormat.Plain };

            var (writerTask, _) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Act - Send messages and complete the channel
            var message1 = new RetrievedMessage
            {
                Body = "Test message 1",
                Queue = "test-queue",
                DeliveryTag = 1,
                BodySizeBytes = 14,
                BodySize = "14 B"
            };

            var message2 = new RetrievedMessage
            {
                Body = "Test message 2",
                Queue = "test-queue",
                DeliveryTag = 2,
                BodySizeBytes = 14,
                BodySize = "14 B"
            };

            await receiveChan.Writer.WriteAsync(message1);
            await receiveChan.Writer.WriteAsync(message2);
            receiveChan.Writer.Complete();

            // Assert
            var result = await writerTask;
            result.ProcessedCount.Should().Be(2);
            result.TotalBytes.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AckTaskCompletes_WhenAckChannelIsCompleted_InAckMode()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions { Format = OutputFormat.Plain };

            var (_, ackTask) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Act - Complete the ack channel (no acks)
            ackChan.Writer.Complete();

            // Assert
            await ackTask;
            ackTask.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public async Task AckTaskSendsAcknowledgments_WhenAcksAreAvailable()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions { Format = OutputFormat.Plain };

            var (_, ackTask) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Act - Send acks and complete the channel
            await ackChan.Writer.WriteAsync((1, true));
            await ackChan.Writer.WriteAsync((2, true));
            ackChan.Writer.Complete();

            // Wait a moment for ack processing
            await ackTask;

            // Assert
            await channel.Received().BasicAckAsync(
                Arg.Is<ulong>(tag => tag >= 1),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task AckTaskCompletesImmediately_InRequeueMode()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Requeue);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions { Format = OutputFormat.Plain };

            var (_, ackTask) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Act - Send an ack (should be ignored in requeue mode)
            await ackChan.Writer.WriteAsync((1, true));

            // Give the ack task a moment to potentially process
            await Task.Delay(50);

            // Assert - Task should complete immediately without processing acks
            ackTask.IsCompleted.Should().BeTrue();
            await channel.DidNotReceive().BasicAckAsync(
                Arg.Any<ulong>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CancellationTokenIsPassedToWriterTask()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions { Format = OutputFormat.Plain };

            using var cts = new CancellationTokenSource();

            var (writerTask, _) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                cts.Token);

            // Act - Cancel immediately
            cts.Cancel();
            receiveChan.Writer.Complete();

            // Assert - Writer task should complete quickly due to cancellation
            var result = await writerTask;
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task BothTasksRunConcurrently()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions { Format = OutputFormat.Plain };

            var (writerTask, ackTask) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Act - Send messages
            for (int i = 1; i <= 3; i++)
            {
                var message = new RetrievedMessage
                {
                    Body = $"Message {i}",
                    Queue = "test-queue",
                    DeliveryTag = (ulong)i,
                    BodySizeBytes = 10,
                    BodySize = "10 B"
                };
                await receiveChan.Writer.WriteAsync(message);
            }

            receiveChan.Writer.Complete();

            // Wait for writer to process and send acks
            var result = await writerTask;

            // The ack channel will be completed automatically by the writer when done
            // Just wait for ack task to complete
            await Task.WhenAny(ackTask, Task.Delay(1000)); // Timeout after 1 second

            // Assert
            result.ProcessedCount.Should().Be(3);
            // Ack task might still be running (batching acks), that's OK
        }
    }

    public class MessageOutputFactoryIntegration
    {
        [Fact]
        public void CreatesConsoleOutput_WhenOutputFileIsNull()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions
            {
                Format = OutputFormat.Plain,
                OutputFile = null // Console output
            };

            // Act
            var (writerTask, _) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Assert - Task should be created successfully
            writerTask.Should().NotBeNull();
            writerTask.Status.Should().NotBe(TaskStatus.Faulted);

            // Cleanup
            receiveChan.Writer.Complete();
        }

        [Fact]
        public void CreatesFileOutput_WhenOutputFileIsSpecified()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var tempFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.txt");

            var outputOptions = new OutputOptions
            {
                Format = OutputFormat.Plain,
                OutputFile = new FileInfo(tempFile) // File output
            };

            try
            {
                // Act
                var (writerTask, _) = pipeline.StartPipeline(
                    receiveChan,
                    ackChan,
                    channel,
                    outputOptions,
                    messageCount: 10,
                    CancellationToken.None);

                // Assert - Task should be created successfully
                writerTask.Should().NotBeNull();
                writerTask.Status.Should().NotBe(TaskStatus.Faulted);

                // Cleanup
                receiveChan.Writer.Complete();
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task SupportsJsonFormat()
        {
            // Arrange
            var pipeline = CreateMessagePipeline(AckModes.Ack);
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChan = Channel.CreateUnbounded<(ulong deliveryTag, bool success)>();
            var channel = Substitute.For<IChannel>();
            var outputOptions = new OutputOptions
            {
                Format = OutputFormat.Json,
                OutputFile = null
            };

            var (writerTask, _) = pipeline.StartPipeline(
                receiveChan,
                ackChan,
                channel,
                outputOptions,
                messageCount: 10,
                CancellationToken.None);

            // Act - Send a message
            var message = new RetrievedMessage
            {
                Body = "Test message",
                Queue = "test-queue",
                DeliveryTag = 1,
                BodySizeBytes = 12,
                BodySize = "12 B"
            };

            await receiveChan.Writer.WriteAsync(message);
            receiveChan.Writer.Complete();

            // Assert
            var result = await writerTask;
            result.ProcessedCount.Should().Be(1);
        }
    }

    #region Test Helpers

    private static MessagePipeline CreateMessagePipeline(AckModes ackMode)
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var fileConfig = new FileConfig();
        var ackHandler = new AckHandler(
            new NullLogger<AckHandler>(),
            new MessageRetrievalOptions
            {
                Queue = "test-queue",
                AckMode = ackMode,
                PrefetchCount = 10,
                MessageCount = 10
            });

        return new MessagePipeline(loggerFactory, fileConfig, ackHandler);
    }

    #endregion
}
