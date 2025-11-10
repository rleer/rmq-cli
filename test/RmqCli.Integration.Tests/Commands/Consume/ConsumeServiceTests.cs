using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Commands.Consume;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared;
using RmqCli.Shared.Output;

namespace RmqCli.Integration.Tests.Commands.Consume;

public class ConsumeServiceTests
{
    public class BeforeRetrievalAsyncLogic
    {
        [Fact]
        public async Task ConfiguresBasicQos_WithPrefetchCount()
        {
            // Arrange
            var (service, mocks) = CreateConsumeService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 5,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 100 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            await channel.Received(1).BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 10,
                global: false,
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ShowsWarning_WhenRequeueModeWithUnlimitedMessageCount()
        {
            // Arrange
            var (service, mocks) = CreateConsumeService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 0, // Unlimited
                AckMode = AckModes.Requeue
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 100 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            mocks.StatusOutput.Received(1).ShowWarning(
                Arg.Is<string>(s => s.Contains("requeue mode") && s.Contains("memory issues")));
        }

        [Fact]
        public async Task ShowsWarning_WhenRequeueModeWithNegativeMessageCount()
        {
            // Arrange
            var (service, mocks) = CreateConsumeService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = -1, // Negative means unlimited
                AckMode = AckModes.Requeue
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 100 };

            // Act
            var result = await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            result.Should().BeTrue();
            mocks.StatusOutput.Received(1).ShowWarning(
                Arg.Is<string>(s => s.Contains("requeue mode") && s.Contains("memory issues")));
        }

        [Fact]
        public async Task DoesNotShowWarning_WhenRequeueModeWithLimitedMessageCount()
        {
            // Arrange
            var (service, mocks) = CreateConsumeService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 50, // Limited
                AckMode = AckModes.Requeue
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
        public async Task DoesNotShowWarning_WhenAckModeWithUnlimitedMessageCount()
        {
            // Arrange
            var (service, mocks) = CreateConsumeService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 0, // Unlimited
                AckMode = AckModes.Ack // Not Requeue
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
        public async Task DoesNotShowWarning_WhenRejectModeWithUnlimitedMessageCount()
        {
            // Arrange
            var (service, mocks) = CreateConsumeService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 10,
                MessageCount = 0, // Unlimited
                AckMode = AckModes.Reject // Not Requeue
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
        public async Task ConfiguresQos_WithCustomPrefetchCount()
        {
            // Arrange
            var (service, mocks) = CreateConsumeService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 100,
                MessageCount = 50,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 100 };

            // Act
            await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            await channel.Received(1).BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 100,
                global: false,
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ConfiguresQos_WithZeroPrefetchCount()
        {
            // Arrange
            var (service, mocks) = CreateConsumeService(new MessageRetrievalOptions
            {
                Queue = "test-queue",
                PrefetchCount = 0, // Unlimited prefetch
                MessageCount = 50,
                AckMode = AckModes.Ack
            });

            var channel = Substitute.For<IChannel>();
            var queueInfo = new QueueInfo { Exists = true, Queue = "test-queue", MessageCount = 100 };

            // Act
            await InvokeBeforeRetrievalAsync(service, channel, queueInfo);

            // Assert
            await channel.Received(1).BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 0,
                global: false,
                Arg.Any<CancellationToken>());
        }
    }

    #region Test Helpers

    private static (ConsumeService service, MockDependencies mocks) CreateConsumeService(MessageRetrievalOptions options)
    {
        var logger = new NullLogger<ConsumeService>();
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

        var service = new ConsumeService(
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
        ConsumeService service,
        IChannel channel,
        QueueInfo queueInfo)
    {
        var method = typeof(ConsumeService)
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
