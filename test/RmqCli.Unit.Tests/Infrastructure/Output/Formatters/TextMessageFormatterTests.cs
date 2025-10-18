using RabbitMQ.Client;
using RmqCli.Commands.Consume;
using RmqCli.Infrastructure.Output.Formatters;

namespace RmqCli.Unit.Tests.Infrastructure.Output.Formatters;

public class TextMessageFormatterTests
{
    #region FormatMessage

    public class FormatMessage
    {
        [Fact]
        public void IncludesMessageDeliveryTagAsHeader()
        {
            // Arrange
            var message = CreateRabbitMessage("test", deliveryTag: 42);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("== Message #42 ==");
        }

        [Fact]
        public void IncludesRedeliveredFlag_WhenTrue()
        {
            // Arrange
            var message = CreateRabbitMessage("test", redelivered: true);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Redelivered: Yes");
        }

        [Fact]
        public void IncludesRedeliveredFlag_WhenFalse()
        {
            // Arrange
            var message = CreateRabbitMessage("test", redelivered: false);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Redelivered: No");
        }

        [Fact]
        public void IncludesExchange()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body", exchange: "test.exchange");

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Exchange: test.exchange");
        }

        [Fact]
        public void IncludesRoutingKey()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body", routingKey: "test.routingKey");

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Routing Key: test.routingKey");
        }

        [Fact]
        public void IncludesBody()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body");

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("== Body (17 bytes) ==\nTest message body");
        }

        [Fact]
        public void HandlesEmptyBody()
        {
            // Arrange
            var message = CreateRabbitMessage("");

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("== Body (0 bytes) ==\n");
            result.Should().EndWith("");
        }

        [Fact]
        public void OmitsProperties_WhenNonePresent()
        {
            // Arrange
            var message = CreateRabbitMessage("test", props: null);

            // Act - use compact mode to omit empty properties
            var result = TextMessageFormatter.FormatMessage(message, compact: true);

            // Assert
            result.Should().NotContain("Message ID:");
            result.Should().NotContain("Content Type:");
            result.Should().NotContain("Type:");
        }

        [Fact]
        public void IncludesMessageId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Message ID: msg-123");
        }

        [Fact]
        public void IncludesContentType_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsContentTypePresent().Returns(true);
            props.ContentType.Returns("application/json");

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Content Type: application/json");
        }

        [Fact]
        public void IncludesAllProperties_WhenAllPresent()
        {
            // Arrange
            var props = CreateFullyPopulatedProperties();
            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("== Properties ==");
            result.Should().Contain("Type: test.type");
            result.Should().Contain("Message ID: msg-001");
            result.Should().Contain("App ID: test-app");
            result.Should().Contain("Cluster ID: cluster-1");
            result.Should().Contain("Content Type: application/json");
            result.Should().Contain("Content Encoding: utf-8");
            result.Should().Contain("Correlation ID: corr-123");
            result.Should().Contain("Delivery Mode: Persistent (2)");
            result.Should().Contain("Expiration: 60000");
            result.Should().Contain("Priority: 5");
            result.Should().Contain("Reply To: reply-queue");
            result.Should().Contain("Timestamp:");
            result.Should().Contain("== Custom Headers ==");
            result.Should().Contain("x-custom: custom-value");
        }

        [Fact]
        public void FormatsHeaders_WithIndentation()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-key1"] = "value1",
                ["x-key2"] = "value2"
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("== Custom Headers ==");
            result.Should().Contain("x-key1: value1");
            result.Should().Contain("x-key2: value2");
        }

        [Fact]
        public void FormatsBinaryData_WithBracketNotation()
        {
            // Arrange
            var binaryData = new byte[] { 0x00, 0x01, 0xFF };
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-binary"] = binaryData
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("x-binary: <binary data: 3 bytes>");
        }

        [Fact]
        public void FormatsNestedArrays_WithIndentation()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-array"] = new object[] { "item1", "item2", 42 }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            // Simple arrays (≤5 items, no complex objects) are formatted inline
            result.Should().Contain("x-array: [item1, item2, 42]");
        }

        [Fact]
        public void FormatsNestedDictionaries_WithIndentation()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-nested"] = new Dictionary<string, object>
                {
                    ["nested-key"] = "nested-value",
                    ["nested-num"] = 99
                }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("x-nested:");
            result.Should().Contain("nested-key: nested-value");
            result.Should().Contain("nested-num: 99");
        }

        [Fact]
        public void PreservesNewlinesInBody()
        {
            // Arrange
            var multilineBody = "Line 1\nLine 2\nLine 3";
            var message = CreateRabbitMessage(multilineBody);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("== Body (20 bytes) ==\nLine 1\nLine 2\nLine 3");
        }
    }

    #endregion

    #region FormatMessages

    public class FormatMessages
    {
        [Fact]
        public void JoinsMultipleMessages_WithNewlines()
        {
            // Arrange
            var messages = new[]
            {
                CreateRabbitMessage("Message 1", exchange: "amq.direct", routingKey: "key.1", deliveryTag: 1),
                CreateRabbitMessage("Message 2", exchange: "amq.topic", routingKey: "key.2", deliveryTag: 2),
                CreateRabbitMessage("Message 3", exchange: "amq.fanout", routingKey: "key.3", deliveryTag: 3)
            };

            // Act
            var result = TextMessageFormatter.FormatMessages(messages);

            // Assert
            result.Should().Contain("Exchange: amq.direct");
            result.Should().Contain("Exchange: amq.topic");
            result.Should().Contain("Exchange: amq.fanout");
            result.Should().Contain("Routing Key: key.1");
            result.Should().Contain("Routing Key: key.2");
            result.Should().Contain("Routing Key: key.3");
            result.Should().Contain("== Message #1 ==");
            result.Should().Contain("== Message #2 ==");
            result.Should().Contain("== Message #3 ==");
            result.Should().Contain("== Body (9 bytes) ==\nMessage 1");
            result.Should().Contain("== Body (9 bytes) ==\nMessage 2");
            result.Should().Contain("== Body (9 bytes) ==\nMessage 3");
        }

        [Fact]
        public void JoinsMessages_WithNewline()
        {
            // Arrange
            var messages = new[]
            {
                CreateRabbitMessage("First", exchange: "amq.direct", routingKey: "key.1", deliveryTag: 1),
                CreateRabbitMessage("Second", exchange: "amq.topic", routingKey: "key.2", deliveryTag: 2)
            };

            // Act
            var result = TextMessageFormatter.FormatMessages(messages);

            // Assert
            result.Should().Contain("== Message #1 ==");
            result.Should().Contain("== Message #2 ==");
            result.Should().Contain("Exchange: amq.direct");
            result.Should().Contain("Exchange: amq.topic");
            result.Should().Contain("Routing Key: key.1");
            result.Should().Contain("Routing Key: key.2");
            result.Should().Contain("== Body (5 bytes) ==\nFirst");
            result.Should().Contain("== Body (6 bytes) ==\nSecond");
        }

        [Fact]
        public void ReturnsEmptyString_WhenNoMessages()
        {
            // Arrange
            var messages = Array.Empty<RabbitMessage>();

            // Act
            var result = TextMessageFormatter.FormatMessages(messages);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void HandlesSingleMessage()
        {
            // Arrange
            var messages = new[] { CreateRabbitMessage("Only one", exchange: "amq.direct", routingKey: "key.1", deliveryTag: 99) };

            // Act
            var result = TextMessageFormatter.FormatMessages(messages);

            // Assert
            result.Should().Contain("== Message #99 ==");
            result.Should().Contain("Exchange: amq.direct");
            result.Should().Contain("Routing Key: key.1");
            result.Should().Contain("== Body (8 bytes) ==\nOnly one");
        }

        [Fact]
        public void IncludesPropertiesInEachMessage()
        {
            // Arrange
            var props1 = Substitute.For<IReadOnlyBasicProperties>();
            props1.IsMessageIdPresent().Returns(true);
            props1.MessageId.Returns("msg-1");

            var props2 = Substitute.For<IReadOnlyBasicProperties>();
            props2.IsMessageIdPresent().Returns(true);
            props2.MessageId.Returns("msg-2");

            var messages = new[]
            {
                CreateRabbitMessage("First", props: props1),
                CreateRabbitMessage("Second", props: props2)
            };

            // Act
            var result = TextMessageFormatter.FormatMessages(messages);

            // Assert
            result.Should().Contain("Message ID: msg-1");
            result.Should().Contain("Message ID: msg-2");
        }
    }

    #endregion

    #region Test Helpers

    private static RabbitMessage CreateRabbitMessage(
        string body,
        string exchange = "exchange",
        string routingKey = "routing.key",
        ulong deliveryTag = 1,
        IReadOnlyBasicProperties? props = null,
        bool redelivered = false)
    {
        return new RabbitMessage(exchange, routingKey, body, deliveryTag, props, redelivered);
    }

    /// <summary>
    /// Creates a mock IReadOnlyBasicProperties with ALL properties populated.
    /// </summary>
    private static IReadOnlyBasicProperties CreateFullyPopulatedProperties()
    {
        var props = Substitute.For<IReadOnlyBasicProperties>();

        props.IsTypePresent().Returns(true);
        props.Type.Returns("test.type");

        props.IsMessageIdPresent().Returns(true);
        props.MessageId.Returns("msg-001");

        props.IsAppIdPresent().Returns(true);
        props.AppId.Returns("test-app");

        props.IsClusterIdPresent().Returns(true);
        props.ClusterId.Returns("cluster-1");

        props.IsContentTypePresent().Returns(true);
        props.ContentType.Returns("application/json");

        props.IsContentEncodingPresent().Returns(true);
        props.ContentEncoding.Returns("utf-8");

        props.IsCorrelationIdPresent().Returns(true);
        props.CorrelationId.Returns("corr-123");

        props.IsDeliveryModePresent().Returns(true);
        props.DeliveryMode.Returns(DeliveryModes.Persistent);

        props.IsExpirationPresent().Returns(true);
        props.Expiration.Returns("60000");

        props.IsPriorityPresent().Returns(true);
        props.Priority.Returns((byte)5);

        props.IsReplyToPresent().Returns(true);
        props.ReplyTo.Returns("reply-queue");

        props.IsTimestampPresent().Returns(true);
        props.Timestamp.Returns(new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

        props.IsHeadersPresent().Returns(true);
        props.Headers.Returns(new Dictionary<string, object?>
        {
            ["x-custom"] = "custom-value"
        });

        return props;
    }

    #endregion
}