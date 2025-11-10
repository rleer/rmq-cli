using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Commands.MessageRetrieval;
using RmqCli.Commands.MessageRetrieval.Strategies;
using RmqCli.Core.Models;

namespace RmqCli.Integration.Tests.Commands.MessageRetrieval.Strategies;

public class PollingStrategyTests
{
    public class RetrieveMessagesAsync
    {
        [Fact]
        public async Task RetrievesMessages_UsingBasicGet()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            var channel = Substitute.For<IChannel>();
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var counter = new ReceivedMessageCounter();

            var result1 = CreateBasicGetResult("Message 1", "test-queue", 1);
            var result2 = CreateBasicGetResult("Message 2", "test-queue", 2);

            channel.BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>())
                .Returns(result1, result2, null); // null indicates no more messages

            // Act
            await strategy.RetrieveMessagesAsync(
                channel,
                "test-queue",
                receiveChan,
                messageCount: 10,
                counter,
                CancellationToken.None);

            // Assert
            counter.Value.Should().Be(2);
            receiveChan.Reader.Count.Should().Be(2);

            // Verify messages were retrieved
            var messages = new List<RetrievedMessage>();
            await foreach (var msg in receiveChan.Reader.ReadAllAsync())
            {
                messages.Add(msg);
            }

            messages.Should().HaveCount(2);
            messages[0].Body.Should().Be("Message 1");
            messages[0].DeliveryTag.Should().Be(1);
            messages[1].Body.Should().Be("Message 2");
            messages[1].DeliveryTag.Should().Be(2);
        }

        [Fact]
        public async Task StopsWhenMessageLimitReached()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            var channel = Substitute.For<IChannel>();
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var counter = new ReceivedMessageCounter();

            var result1 = CreateBasicGetResult("Message 1", "test-queue", 1);
            var result2 = CreateBasicGetResult("Message 2", "test-queue", 2);
            var result3 = CreateBasicGetResult("Message 3", "test-queue", 3);

            channel.BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>())
                .Returns(result1, result2, result3);

            // Act - limit to 2 messages
            await strategy.RetrieveMessagesAsync(
                channel,
                "test-queue",
                receiveChan,
                messageCount: 2,
                counter,
                CancellationToken.None);

            // Assert
            counter.Value.Should().Be(2);

            // Should only call BasicGetAsync twice
            await channel.Received(2).BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task StopsWhenQueueIsEmpty()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            var channel = Substitute.For<IChannel>();
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var counter = new ReceivedMessageCounter();

            var result1 = CreateBasicGetResult("Message 1", "test-queue", 1);

            channel.BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>())
                .Returns(result1, (BasicGetResult?)null); // Empty after first message

            // Act
            await strategy.RetrieveMessagesAsync(
                channel,
                "test-queue",
                receiveChan,
                messageCount: 100,
                counter,
                CancellationToken.None);

            // Assert
            counter.Value.Should().Be(1);
            receiveChan.Reader.Count.Should().Be(1);
        }

        [Fact]
        public async Task CompletesChannelAfterRetrieving()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            var channel = Substitute.For<IChannel>();
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var counter = new ReceivedMessageCounter();

            channel.BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>())
                .Returns((BasicGetResult?)null); // Empty queue

            // Act
            await strategy.RetrieveMessagesAsync(
                channel,
                "test-queue",
                receiveChan,
                messageCount: 10,
                counter,
                CancellationToken.None);

            // Assert
            receiveChan.Reader.Completion.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public async Task HandlesRedeliveredMessages()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            var channel = Substitute.For<IChannel>();
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var counter = new ReceivedMessageCounter();

            var result = CreateBasicGetResult("Redelivered message", "test-queue", 1, redelivered: true);

            channel.BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>())
                .Returns(result, (BasicGetResult?)null);

            // Act
            await strategy.RetrieveMessagesAsync(
                channel,
                "test-queue",
                receiveChan,
                messageCount: 10,
                counter,
                CancellationToken.None);

            // Assert
            var messages = new List<RetrievedMessage>();
            await foreach (var msg in receiveChan.Reader.ReadAllAsync())
            {
                messages.Add(msg);
            }

            messages.Should().HaveCount(1);
            messages[0].Redelivered.Should().BeTrue();
        }

        [Fact]
        public async Task ExtractsMessageProperties()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            var channel = Substitute.For<IChannel>();
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var counter = new ReceivedMessageCounter();

            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");
            props.IsContentTypePresent().Returns(true);
            props.ContentType.Returns("application/json");

            var result = CreateBasicGetResult("Test message", "test-queue", 1, properties: props);

            channel.BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>())
                .Returns(result, (BasicGetResult?)null);

            // Act
            await strategy.RetrieveMessagesAsync(
                channel,
                "test-queue",
                receiveChan,
                messageCount: 10,
                counter,
                CancellationToken.None);

            // Assert
            var messages = new List<RetrievedMessage>();
            await foreach (var msg in receiveChan.Reader.ReadAllAsync())
            {
                messages.Add(msg);
            }

            messages.Should().HaveCount(1);
            messages[0].Properties.Should().NotBeNull();
            messages[0].Properties!.MessageId.Should().Be("msg-123");
            messages[0].Properties!.ContentType.Should().Be("application/json");
        }

        [Fact]
        public async Task HandlesCancellation()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            var channel = Substitute.For<IChannel>();
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var counter = new ReceivedMessageCounter();

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            var result = CreateBasicGetResult("Message", "test-queue", 1);
            channel.BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>())
                .Returns(result);

            // Act
            await strategy.RetrieveMessagesAsync(
                channel,
                "test-queue",
                receiveChan,
                messageCount: 10,
                counter,
                cts.Token);

            // Assert
            counter.Value.Should().Be(0); // No messages should be processed
            await channel.DidNotReceive().BasicGetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SetsCorrectMetadata()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            var channel = Substitute.For<IChannel>();
            var receiveChan = Channel.CreateUnbounded<RetrievedMessage>();
            var counter = new ReceivedMessageCounter();

            var result = CreateBasicGetResult("Test", "my-queue", 42, exchange: "my-exchange", routingKey: "my.routing.key");

            channel.BasicGetAsync("my-queue", false, Arg.Any<CancellationToken>())
                .Returns(result, (BasicGetResult?)null);

            // Act
            await strategy.RetrieveMessagesAsync(
                channel,
                "my-queue",
                receiveChan,
                messageCount: 10,
                counter,
                CancellationToken.None);

            // Assert
            var messages = new List<RetrievedMessage>();
            await foreach (var msg in receiveChan.Reader.ReadAllAsync())
            {
                messages.Add(msg);
            }

            messages.Should().HaveCount(1);
            messages[0].Queue.Should().Be("my-queue");
            messages[0].Exchange.Should().Be("my-exchange");
            messages[0].RoutingKey.Should().Be("my.routing.key");
            messages[0].DeliveryTag.Should().Be(42);
            messages[0].BodySize.Should().NotBeNullOrEmpty();
            messages[0].BodySizeBytes.Should().Be(4); // "Test" = 4 bytes
        }
    }

    public class PropertiesTests
    {
        [Fact]
        public void StrategyName_ReturnsPolling()
        {
            // Arrange
            var logger = new NullLogger<PollingStrategy>();
            var strategy = new PollingStrategy(logger);

            // Act & Assert
            strategy.StrategyName.Should().Be("Polling");
        }
    }

    #region Test Helpers

    private static BasicGetResult CreateBasicGetResult(
        string body,
        string queue,
        ulong deliveryTag,
        bool redelivered = false,
        string exchange = "",
        string routingKey = "",
        IReadOnlyBasicProperties? properties = null)
    {
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
        var props = properties ?? Substitute.For<IReadOnlyBasicProperties>();

        return new BasicGetResult(
            deliveryTag,
            redelivered,
            exchange,
            routingKey,
            messageCount: 0,
            basicProperties: props,
            body: bodyBytes);
    }

    #endregion
}
