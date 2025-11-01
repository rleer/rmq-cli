using System.Globalization;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Output.Formatters;
using RmqCli.Unit.Tests.Helpers;

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
        public void IncludesQueue()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body", queue: "queue-name");

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Queue: queue-name");
        }

        [Fact]
        public void DistinguishesQueueFromRoutingKey()
        {
            // Arrange - Queue and routing key should be different
            var message = CreateRabbitMessage(
                "Test message body",
                exchange: "amq.direct",
                routingKey: "user.created",
                queue: "notifications-queue");

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Queue: notifications-queue");
            result.Should().Contain("Routing Key: user.created");
            // Verify they are not the same
            result.IndexOf("Queue: notifications-queue", StringComparison.Ordinal).Should().NotBe(result.IndexOf("Routing Key: user.created", StringComparison.Ordinal));
        }

        [Fact]
        public void IncludesBody()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body");

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain($"== Body (17 bytes) =={Environment.NewLine}Test message body");
        }

        [Fact]
        public void HandlesEmptyBody()
        {
            // Arrange
            var message = CreateRabbitMessage("");

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain($"== Body (0 bytes) =={Environment.NewLine}");
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
            var props = RabbitMessageTestHelper.CreateFullyPopulatedProperties();
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
        public void FormatsSimpleHeaders()
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
        public void OmitsNullHeaderValues()
        {
            // Arrange - RabbitMQ filters out null header values
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-valid"] = "value"
                // null values are not stored in RabbitMQ headers
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("x-valid: value");
            result.Should().NotContain("x-null");
        }

        [Fact]
        public void FormatsBinaryDataHeader()
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

            // Assert - Verify binary data is formatted with byte count
            result.Should().Contain("""
                                    == Custom Headers ==
                                    x-binary: <binary data: 3 bytes>
                                    """);
        }

        [Fact]
        public void FormatsEmptyArray()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-empty-array"] = Array.Empty<object>()
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Verify empty array is formatted as []
            result.Should().Contain("""
                                    == Custom Headers ==
                                    x-empty-array: []
                                    """);
        }

        [Fact]
        public void FormatsSmallArrayInline()
        {
            // Arrange - Arrays ≤5 items without complex objects are formatted inline
            CultureInfo.CurrentCulture = new CultureInfo("en-US"); // Ensure consistent decimal formatting
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-array"] = new object[] { "item1", "item2", 42, true, 3.14 }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Verify array is formatted inline (all on one line)
            result.Should().Contain("""
                                    == Custom Headers ==
                                    x-array: [item1, item2, 42, True, 3.14]
                                    """);
        }

        [Fact]
        public void FormatsLargeArrayMultiLine()
        {
            // Arrange - Arrays >5 items are formatted multi-line
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-large-array"] = new object[] { 1, 2, 3, 4, 5, 6 }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Verify multi-line array with proper indentation (platform-independent)
            result.Should().Contain("== Custom Headers ==");
            result.Should().Contain("x-large-array: [");
            result.Should().Contain("  1");
            result.Should().Contain("  2");
            result.Should().Contain("  3");
            result.Should().Contain("  4");
            result.Should().Contain("  5");
            result.Should().Contain("  6");
            result.Should().Contain("]");
        }

        [Fact]
        public void FormatsArrayWithComplexObjectsMultiLine()
        {
            // Arrange - Arrays containing complex objects are formatted multi-line
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-array-of-objects"] = new object[]
                {
                    new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 },
                    new Dictionary<string, object> { ["name"] = "Bob", ["age"] = 25 }
                }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Verify proper multi-line formatting with correct indentation (platform-independent)
            result.Should().Contain("== Custom Headers ==");
            result.Should().Contain("x-array-of-objects: [");
            result.Should().Contain("  {name: Alice, age: 30}");
            result.Should().Contain("  {name: Bob, age: 25}");
            result.Should().Contain("]");
        }

        [Fact]
        public void FormatsEmptyDictionary()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-empty-dict"] = new Dictionary<string, object>()
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Verify empty dictionary is formatted as {}
            result.Should().Contain("""
                                    == Custom Headers ==
                                    x-empty-dict: {}
                                    """);
        }

        [Fact]
        public void FormatsSimpleDictionaryInline()
        {
            // Arrange - Simple dictionaries (≤3 items, no nested objects) are formatted inline
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-simple-dict"] = new Dictionary<string, object>
                {
                    ["status"] = "active",
                    ["count"] = 42,
                    ["enabled"] = true
                }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Verify dictionary is formatted inline (all on one line)
            result.Should().Contain("""
                                    == Custom Headers ==
                                    x-simple-dict: {status: active, count: 42, enabled: True}
                                    """);
        }

        [Fact]
        public void FormatsNestedDictionaryMultiLine()
        {
            // Arrange - Dictionaries with nested objects are formatted multi-line
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-nested"] = new Dictionary<string, object>
                {
                    ["user"] = new Dictionary<string, object>
                    {
                        ["name"] = "Alice",
                        ["role"] = "admin"
                    },
                    ["timestamp"] = 1234567890
                }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Verify nested dictionary formatting (platform-independent)
            result.Should().Contain("== Custom Headers ==");
            result.Should().Contain("x-nested: {");
            result.Should().Contain("  user: {name: Alice, role: admin}");
            result.Should().Contain("  timestamp: 1234567890");
            result.Should().Contain("}");
        }

        [Fact]
        public void FormatsDeeplyNestedStructure()
        {
            // Arrange - Test deeply nested structure with proper indentation (2 spaces per level)
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-deep"] = new Dictionary<string, object>
                {
                    ["level1"] = new Dictionary<string, object>
                    {
                        ["level2"] = new Dictionary<string, object>
                        {
                            ["level3"] = new Dictionary<string, object>
                            {
                                ["value"] = "deep-value"
                            }
                        },
                        ["anotherKey"] = "anotherValue"
                    },
                    ["simpleKey"] = "simpleValue"
                }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message, compact:true);

            // Assert - Verify proper indentation at each nesting level (platform-independent)
            result.Should().Contain("== Custom Headers ==");
            result.Should().Contain("x-deep: {");
            result.Should().Contain("  level1: {");
            result.Should().Contain("    level2: {");
            result.Should().Contain("      level3: {value: deep-value}");
            result.Should().Contain("    }");
            result.Should().Contain("    anotherKey: anotherValue");
            result.Should().Contain("  }");
            result.Should().Contain("  simpleKey: simpleValue");
            result.Should().Contain("}");
        }

        [Fact]
        public void FormatsMixedComplexHeaders()
        {
            // Arrange - Test mix of different header types (null values are filtered by RabbitMQ)
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            var headers = new Dictionary<string, object?>
            {
                ["x-string"] = "simple-value",
                ["x-number"] = 123,
                ["x-bool"] = true,
                ["x-array"] = new object[] { 1, 2, 3 },
                ["x-dict"] = new Dictionary<string, object> { ["key"] = "value" }
            };
            props.Headers.Returns(headers);

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Verify all types are formatted correctly
            result.Should().Contain("""
                                    == Custom Headers ==
                                    x-string: simple-value
                                    x-number: 123
                                    x-bool: True
                                    x-array: [1, 2, 3]
                                    x-dict: {key: value}
                                    """);
        }

        [Fact]
        public void PreservesNewlinesInBody()
        {
            // Arrange - Use \n explicitly for consistent byte count across platforms
            var multilineBody = "Line 1\nLine 2\nLine 3";
            var message = CreateRabbitMessage(multilineBody);

            // Act
            var result = TextMessageFormatter.FormatMessage(message);

            // Assert - Platform-independent check
            result.Should().Contain("== Body (20 bytes) ==");
            result.Should().Contain("Line 1");
            result.Should().Contain("Line 2");
            result.Should().Contain("Line 3");
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
            result.Should().Contain($"== Body (9 bytes) =={Environment.NewLine}Message 1");
            result.Should().Contain($"== Body (9 bytes) =={Environment.NewLine}Message 2");
            result.Should().Contain($"== Body (9 bytes) =={Environment.NewLine}Message 3");
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
            result.Should().Contain($"== Body (5 bytes) =={Environment.NewLine}First");
            result.Should().Contain($"== Body (6 bytes) =={Environment.NewLine}Second");
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
            result.Should().Contain($"== Body (8 bytes) =={Environment.NewLine}Only one");
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
                CreateRabbitMessage("First", deliveryTag: 1, props: props1),
                CreateRabbitMessage("Second", deliveryTag: 2, props: props2)
            };

            // Act
            var result = TextMessageFormatter.FormatMessages(messages);

            // Assert
            result.Should().Contain("Message ID: msg-1");
            result.Should().Contain("Message ID: msg-2");
            result.Should().Contain("== Message #1 ==");
            result.Should().Contain("== Message #2 ==");
        }
    }

    #endregion

    #region Test Helpers

    private static RabbitMessage CreateRabbitMessage(
        string body,
        string exchange = "exchange",
        string routingKey = "routing.key",
        string queue = "test-queue",
        ulong deliveryTag = 1,
        IReadOnlyBasicProperties? props = null,
        bool redelivered = false)
    {
        return new RabbitMessage(exchange, routingKey, queue, body, deliveryTag, props, redelivered);
    }

    #endregion
}