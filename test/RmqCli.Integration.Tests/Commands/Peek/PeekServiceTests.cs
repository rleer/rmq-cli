using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Commands.Peek;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared;
using RmqCli.Shared.Output;

namespace RmqCli.Integration.Tests.Commands.Peek;

public class PeekServiceTests
{
    public class BeforeRetrievalAsyncLogic
    {
        [Fact]
        public async Task ReturnsFalse_WhenQueueIsEmpty()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 5,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 0 }; // Empty queue

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeFalse();
            mocks.StatusOutput.Received(1).ShowWarning(
                Arg.Is<string>(s => s.Contains("empty") && s.Contains("rmq consume")));
        }

        [Fact]
        public async Task ReturnsTrue_WhenQueueHasMessages()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 5,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 50 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            mocks.StatusOutput.DidNotReceive().ShowWarning(Arg.Any<string>());
        }

        [Fact]
        public async Task ShowsWarning_WhenMessageCountIs1000()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 1000, // Exactly 1000
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 2000 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            mocks.StatusOutput.Received(1).ShowWarning(
                Arg.Is<string>(s => s.Contains("polling") && s.Contains("inefficient") && s.Contains("rmq consume")));
        }

        [Fact]
        public async Task ShowsWarning_WhenMessageCountIsGreaterThan1000()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 5000,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 10000 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            mocks.StatusOutput.Received(1).ShowWarning(
                Arg.Is<string>(s => s.Contains("5000") && s.Contains("polling") && s.Contains("inefficient")));
        }

        [Fact]
        public async Task DoesNotShowWarning_WhenMessageCountIsLessThan1000()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 999,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 2000 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            mocks.StatusOutput.DidNotReceive().ShowWarning(Arg.Any<string>());
        }

        [Fact]
        public async Task DoesNotShowWarning_WhenMessageCountIsSmall()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 10,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 100 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            mocks.StatusOutput.DidNotReceive().ShowWarning(Arg.Any<string>());
        }

        [Fact]
        public async Task ReturnsFalse_AndShowsWarning_ForEmptyQueue_RegardlessOfMessageCount()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 5000, // Large count, but queue is empty
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 0 }; // Empty

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeFalse();
            // Should only show empty queue warning, not the large message count warning (since we return early)
            mocks.StatusOutput.Received(1).ShowWarning(
                Arg.Is<string>(s => s.Contains("empty")));
        }

        [Fact]
        public async Task ReturnsTrue_ForQueueWithOneMessage()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 1,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 1 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            mocks.StatusOutput.DidNotReceive().ShowWarning(Arg.Any<string>());
        }

        [Fact]
        public async Task WarningMessage_ContainsMessageCount()
        {
            // Arrange
            var (service, mocks) = CreatePeekService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 2500,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 5000 };

            // Act
            await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            mocks.StatusOutput.Received(1).ShowWarning(
                Arg.Is<string>(s => s.Contains("2500"))); // Should mention the specific count
        }
    }

    #region Test Helpers

    private static (PeekService service, MockDependencies mocks) CreatePeekService(MessageRetrievalOptions options)
    {
        var logger = new NullLogger<PeekService>();
        var statusOutput = Substitute.For<IStatusOutputService>();

        // Create actual instances since we're not testing these directly
        var queueValidator = new QueueValidator(new NullLogger<QueueValidator>());
        var ackHandler = new AckHandler(new NullLogger<AckHandler>(), options);
        var messagePipeline = new MessagePipeline(
            NullLoggerFactory.Instance,
            new RmqCli.Infrastructure.Configuration.Models.FileConfig(),
            ackHandler);

        var outputOptions = new OutputOptions { Format = OutputFormat.Plain };
        var rabbitChannelFactory = Substitute.For<IRabbitChannelFactory>();
        var resultOutput = new MessageRetrievalResultOutputService(outputOptions);
        var strategy = Substitute.For<IMessageRetrievalStrategy>();

        var service = new PeekService(
            logger,
            statusOutput,
            queueValidator,
            messagePipeline,
            outputOptions,
            rabbitChannelFactory,
            resultOutput,
            options,
            strategy);

        return (service, new MockDependencies
        {
            StatusOutput = statusOutput,
            QueueValidator = queueValidator,
            MessagePipeline = messagePipeline,
            RabbitChannelFactory = rabbitChannelFactory,
            ResultOutput = resultOutput,
            Strategy = strategy
        });
    }

    /// <summary>
    /// Uses reflection to invoke the protected BeforeRetrievalAsync method for testing
    /// </summary>
    private static async Task<bool> InvokeBeforeRetrievalAsync(
        PeekService service,
        IChannel channel,
        QueueInfo queueInfo)
    {
        var method = typeof(PeekService)
            .GetMethod("BeforeRetrievalAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull("BeforeRetrievalAsync method should exist");

        var task = method.Invoke(service, [channel, queueInfo, CancellationToken.None]) as Task<bool>;
        return await task!;
    }

    private record MockDependencies
    {
        public required IStatusOutputService StatusOutput { get; init; }
        public required QueueValidator QueueValidator { get; init; }
        public required MessagePipeline MessagePipeline { get; init; }
        public required IRabbitChannelFactory RabbitChannelFactory { get; init; }
        public required MessageRetrievalResultOutputService ResultOutput { get; init; }
        public required IMessageRetrievalStrategy Strategy { get; init; }
    }

    #endregion
}
