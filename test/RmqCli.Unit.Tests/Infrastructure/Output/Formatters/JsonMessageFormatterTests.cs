using System.Text.Json;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Output.Formatters;
using RmqCli.Unit.Tests.Helpers;

namespace RmqCli.Unit.Tests.Infrastructure.Output.Formatters;

public class JsonMessageFormatterTests
{
    #region FormatMessage

    public class FormatMessage
    {
        [Fact]
        public void ReturnsValidJson_ForSimpleMessage()
        {
            // Arrange
            var message = CreateRabbitMessage("Hello, World!", exchange: "exchange", routingKey: "key", deliveryTag: 1);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().NotBeNullOrEmpty();
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("deliveryTag").GetUInt64().Should().Be(1);
            json.RootElement.GetProperty("body").GetString().Should().Be("Hello, World!");
            json.RootElement.GetProperty("exchange").GetString().Should().Be("exchange");
            json.RootElement.GetProperty("routingKey").GetString().Should().Be("key");
        }

        [Fact]
        public void IncludesDeliveryTag()
        {
            // Arrange
            var message = CreateRabbitMessage("test", deliveryTag: 42);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("deliveryTag").GetUInt64().Should().Be(42);
        }

        [Fact]
        public void IncludesRedeliveredFlag_WhenTrue()
        {
            // Arrange
            var message = CreateRabbitMessage("test", redelivered: true);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("redelivered").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public void IncludesRedeliveredFlag_WhenFalse()
        {
            // Arrange
            var message = CreateRabbitMessage("test", redelivered: false);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("redelivered").GetBoolean().Should().BeFalse();
        }
        
        [Fact]
        public void IncludesExchange()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body", exchange: "amq.direct");

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("exchange").GetString().Should().Be("amq.direct");
        }
        
        [Fact]
        public void IncludesRoutingKey()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body", routingKey: "my.routing.key");

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("routingKey").GetString().Should().Be("my.routing.key");
        }

        [Fact]
        public void IncludesQueue()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body", routingKey: "test.queue");

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("queue").GetString().Should().Be("test-queue");
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
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("queue").GetString().Should().Be("notifications-queue");
            json.RootElement.GetProperty("routingKey").GetString().Should().Be("user.created");
            // Verify they are not the same
            json.RootElement.GetProperty("queue").GetString().Should().NotBe(json.RootElement.GetProperty("routingKey").GetString());
        }

        [Fact]
        public void IncludesBody()
        {
            // Arrange
            var message = CreateRabbitMessage("Test message body");

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("body").GetString().Should().Be("Test message body");
        }

        [Fact]
        public void HandlesEmptyBody()
        {
            // Arrange
            var message = CreateRabbitMessage("");

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("body").GetString().Should().BeEmpty();
        }

        [Fact]
        public void OmitsProperties_WhenNonePresent()
        {
            // Arrange
            var propsWithNoValues = Substitute.For<IReadOnlyBasicProperties>();
            // All IsXxxPresent() return false by default
            var message = CreateRabbitMessage("test", props: propsWithNoValues);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.TryGetProperty("properties", out var properties).Should().BeTrue();
            properties.ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Fact]
        public void IncludesProperties_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");
            props.IsContentTypePresent().Returns(true);
            props.ContentType.Returns("application/json");

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.TryGetProperty("properties", out var properties).Should().BeTrue();
            properties.GetProperty("messageId").GetString().Should().Be("msg-123");
            properties.GetProperty("contentType").GetString().Should().Be("application/json");
        }

        [Fact]
        public void IncludesAllProperties_WhenAllPresent()
        {
            // Arrange
            var props = RabbitMessageTestHelper.CreateFullyPopulatedProperties();
            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.TryGetProperty("properties", out var properties).Should().BeTrue();

            // Verify all 13 properties are present
            properties.GetProperty("type").GetString().Should().Be("test.type");
            properties.GetProperty("messageId").GetString().Should().Be("msg-001");
            properties.GetProperty("appId").GetString().Should().Be("test-app");
            properties.GetProperty("clusterId").GetString().Should().Be("cluster-1");
            properties.GetProperty("contentType").GetString().Should().Be("application/json");
            properties.GetProperty("contentEncoding").GetString().Should().Be("utf-8");
            properties.GetProperty("correlationId").GetString().Should().Be("corr-123");
            properties.GetProperty("deliveryMode").GetInt32().Should().Be(2); // Persistent
            properties.GetProperty("expiration").GetString().Should().Be("60000");
            properties.GetProperty("priority").GetByte().Should().Be(5);
            properties.GetProperty("replyTo").GetString().Should().Be("reply-queue");
            properties.GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();

            properties.TryGetProperty("headers", out var headers).Should().BeTrue();
            headers.GetProperty("x-custom").GetString().Should().Be("custom-value");
        }

        [Fact]
        public void HandlesSpecialCharactersInBody()
        {
            // Arrange
            var specialBody = "Line1\nLine2\tTabbed\r\nQuotes:\"test\"";
            var message = CreateRabbitMessage(specialBody);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("body").GetString().Should().Be(specialBody);
        }

        [Fact]
        public void HandlesUnicodeInBody()
        {
            // Arrange
            var unicodeBody = "Hello 世界 🌍 مرحبا";
            var message = CreateRabbitMessage(unicodeBody);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("body").GetString().Should().Be(unicodeBody);
        }

        [Fact]
        public void IncludesHeaders_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-custom"] = "custom-value",
                ["x-number"] = 42
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("properties").TryGetProperty("headers", out var headers).Should().BeTrue();
            headers.GetProperty("x-custom").GetString().Should().Be("custom-value");
            headers.GetProperty("x-number").GetInt32().Should().Be(42);
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
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            var headers = json.RootElement.GetProperty("properties").GetProperty("headers");
            headers.TryGetProperty("x-valid", out var validProp).Should().BeTrue();
            validProp.GetString().Should().Be("value");
            headers.TryGetProperty("x-null", out _).Should().BeFalse();
        }

        [Fact]
        public void FormatsBooleanHeaderValue()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-enabled"] = true,
                ["x-disabled"] = false
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            var headers = json.RootElement.GetProperty("properties").GetProperty("headers");
            headers.GetProperty("x-enabled").GetBoolean().Should().BeTrue();
            headers.GetProperty("x-disabled").GetBoolean().Should().BeFalse();
        }

        [Fact]
        public void FormatsArrayHeaderValue()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-tags"] = new object[] { "tag1", "tag2", "tag3" }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            var tags = json.RootElement.GetProperty("properties").GetProperty("headers").GetProperty("x-tags");
            tags.ValueKind.Should().Be(JsonValueKind.Array);
            tags.GetArrayLength().Should().Be(3);
            tags[0].GetString().Should().Be("tag1");
            tags[1].GetString().Should().Be("tag2");
            tags[2].GetString().Should().Be("tag3");
        }

        [Fact]
        public void FormatsNestedDictionaryHeaderValue()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-metadata"] = new Dictionary<string, object>
                {
                    ["region"] = "us-west",
                    ["datacenter"] = "dc1",
                    ["rack"] = 42
                }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            var metadata = json.RootElement.GetProperty("properties").GetProperty("headers").GetProperty("x-metadata");
            metadata.ValueKind.Should().Be(JsonValueKind.Object);
            metadata.GetProperty("region").GetString().Should().Be("us-west");
            metadata.GetProperty("datacenter").GetString().Should().Be("dc1");
            metadata.GetProperty("rack").GetInt32().Should().Be(42);
        }

        [Fact]
        public void FormatsDeeplyNestedHeaderStructure()
        {
            // Arrange
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
                            ["level3"] = "deep-value"
                        }
                    }
                }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            var deepValue = json.RootElement
                .GetProperty("properties")
                .GetProperty("headers")
                .GetProperty("x-deep")
                .GetProperty("level1")
                .GetProperty("level2")
                .GetProperty("level3")
                .GetString();
            deepValue.Should().Be("deep-value");
        }

        [Fact]
        public void FormatsArrayOfObjectsHeaderValue()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-users"] = new object[]
                {
                    new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 },
                    new Dictionary<string, object> { ["name"] = "Bob", ["age"] = 25 }
                }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            var users = json.RootElement.GetProperty("properties").GetProperty("headers").GetProperty("x-users");
            users.ValueKind.Should().Be(JsonValueKind.Array);
            users.GetArrayLength().Should().Be(2);
            users[0].GetProperty("name").GetString().Should().Be("Alice");
            users[0].GetProperty("age").GetInt32().Should().Be(30);
            users[1].GetProperty("name").GetString().Should().Be("Bob");
            users[1].GetProperty("age").GetInt32().Should().Be(25);
        }

        [Fact]
        public void FormatsMixedTypeHeaderValues()
        {
            // Arrange - RabbitMQ filters out null values
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-string"] = "text",
                ["x-int"] = 123,
                ["x-long"] = 9876543210L,
                ["x-double"] = 3.14,
                ["x-bool"] = true,
                ["x-array"] = new object[] { 1, "two", 3.0 }
            });

            var message = CreateRabbitMessage("test", props: props);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            var headers = json.RootElement.GetProperty("properties").GetProperty("headers");
            headers.GetProperty("x-string").GetString().Should().Be("text");
            headers.GetProperty("x-int").GetInt32().Should().Be(123);
            headers.GetProperty("x-long").GetInt64().Should().Be(9876543210L);
            headers.GetProperty("x-double").GetDouble().Should().BeApproximately(3.14, 0.001);
            headers.GetProperty("x-bool").GetBoolean().Should().BeTrue();
            headers.GetProperty("x-array").GetArrayLength().Should().Be(3);
        }

        [Fact]
        public void IncludesBodySize_InBytesAndFormatted()
        {
            // Arrange
            var message = CreateRabbitMessage("Hello, World!");

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("bodySizeBytes").GetInt32().Should().Be(13);
            json.RootElement.GetProperty("bodySize").GetString().Should().Be("13 bytes");
        }

        [Fact]
        public void IncludesBodySize_ForLargerMessages()
        {
            // Arrange
            var largeBody = new string('a', 2048);
            var message = CreateRabbitMessage(largeBody);

            // Act
            var result = JsonMessageFormatter.FormatMessage(message);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("bodySizeBytes").GetInt32().Should().Be(2048);
            json.RootElement.GetProperty("bodySize").GetString().Should().Be("2 KB");
        }
    }

    #endregion

    #region FormatMessages

    public class FormatMessages
    {
        [Fact]
        public void ReturnsValidJsonArray_ForMultipleMessages()
        {
            // Arrange
            var messages = new[]
            {
                CreateRabbitMessage("Message 1", deliveryTag: 1),
                CreateRabbitMessage("Message 2", deliveryTag: 2),
                CreateRabbitMessage("Message 3", deliveryTag: 3)
            };

            // Act
            var result = JsonMessageFormatter.FormatMessages(messages);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.TryGetProperty("messages", out var messagesArray).Should().BeTrue();
            messagesArray.ValueKind.Should().Be(JsonValueKind.Array);
            messagesArray.GetArrayLength().Should().Be(3);
        }

        [Fact]
        public void IncludesAllMessages()
        {
            // Arrange
            var messages = new[]
            {
                CreateRabbitMessage("First", "exchange1", "routing.key.1", deliveryTag: 1),
                CreateRabbitMessage("Second", "exchange2", "routing.key.2", deliveryTag: 2)
            };

            // Act
            var result = JsonMessageFormatter.FormatMessages(messages);

            // Assert
            var json = JsonDocument.Parse(result);
            var array = json.RootElement.GetProperty("messages").EnumerateArray().ToList();

            array[0].GetProperty("body").GetString().Should().Be("First");
            array[0].GetProperty("deliveryTag").GetUInt64().Should().Be(1);
            array[0].GetProperty("exchange").GetString().Should().Be("exchange1");
            array[0].GetProperty("routingKey").GetString().Should().Be("routing.key.1");

            array[1].GetProperty("body").GetString().Should().Be("Second");
            array[1].GetProperty("deliveryTag").GetUInt64().Should().Be(2);
            array[1].GetProperty("exchange").GetString().Should().Be("exchange2");
            array[1].GetProperty("routingKey").GetString().Should().Be("routing.key.2");
        }

        [Fact]
        public void ReturnsEmptyArray_WhenNoMessages()
        {
            // Arrange
            var messages = Array.Empty<RabbitMessage>();

            // Act
            var result = JsonMessageFormatter.FormatMessages(messages);

            // Assert
            var json = JsonDocument.Parse(result);
            json.RootElement.GetProperty("messages").ValueKind.Should().Be(JsonValueKind.Array);
            json.RootElement.GetProperty("messages").GetArrayLength().Should().Be(0);
        }

        [Fact]
        public void HandlesSingleMessage()
        {
            // Arrange
            var messages = new[] { CreateRabbitMessage("Only one", "amq.direct", "key", deliveryTag: 99) };

            // Act
            var result = JsonMessageFormatter.FormatMessages(messages);

            // Assert
            var json = JsonDocument.Parse(result);
            var messagesArray = json.RootElement.GetProperty("messages");
            messagesArray.GetArrayLength().Should().Be(1);
            messagesArray[0].GetProperty("body").GetString().Should().Be("Only one");
            messagesArray[0].GetProperty("deliveryTag").GetUInt64().Should().Be(99);
            messagesArray[0].GetProperty("exchange").GetString().Should().Be("amq.direct");
            messagesArray[0].GetProperty("routingKey").GetString().Should().Be("key");
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
            var result = JsonMessageFormatter.FormatMessages(messages);

            // Assert
            var json = JsonDocument.Parse(result);
            var array = json.RootElement.GetProperty("messages").EnumerateArray().ToList();

            array[0].GetProperty("properties").GetProperty("messageId").GetString().Should().Be("msg-1");
            array[1].GetProperty("properties").GetProperty("messageId").GetString().Should().Be("msg-2");
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