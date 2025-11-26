using System.Text.Json;
using RmqCli.Core.Models;
using RmqCli.Shared.Json;
using RmqCli.Shared.Output;

namespace RmqCli.Unit.Tests.Core.Models;

public class RetrievedMessageTests
{
    public class RecordBehavior
    {
        [Fact]
        public void IsRecordType()
        {
            // Arrange & Act
            var message = new RetrievedMessage();

            // Assert
            message.Should().BeAssignableTo<RetrievedMessage>();
        }

        [Fact]
        public void SupportsWithExpression()
        {
            // Arrange
            var original = new RetrievedMessage
            {
                Body = "Original",
                Queue = "queue1"
            };

            // Act
            var modified = original with { Body = "Modified" };

            // Assert
            modified.Body.Should().Be("Modified");
            modified.Queue.Should().Be("queue1");
            original.Body.Should().Be("Original");
        }

        [Fact]
        public void Equality_ComparesAllProperties()
        {
            // Arrange
            var message1 = new RetrievedMessage
            {
                Body = "test",
                Queue = "queue1",
                RoutingKey = "key",
                Exchange = "exchange",
                DeliveryTag = 1,
                Redelivered = false,
                BodySizeBytes = 4,
                BodySize = "4 bytes"
            };

            var message2 = new RetrievedMessage
            {
                Body = "test",
                Queue = "queue1",
                RoutingKey = "key",
                Exchange = "exchange",
                DeliveryTag = 1,
                Redelivered = false,
                BodySizeBytes = 4,
                BodySize = "4 bytes"
            };

            // Act & Assert
            message1.Should().Be(message2);
            (message1 == message2).Should().BeTrue();
        }

        [Fact]
        public void Inequality_WhenBodyDiffers()
        {
            // Arrange
            var message1 = new RetrievedMessage { Body = "test1" };
            var message2 = new RetrievedMessage { Body = "test2" };

            // Act & Assert
            message1.Should().NotBe(message2);
        }

        [Fact]
        public void Inequality_WhenDeliveryTagDiffers()
        {
            // Arrange
            var message1 = new RetrievedMessage { DeliveryTag = 1 };
            var message2 = new RetrievedMessage { DeliveryTag = 2 };

            // Act & Assert
            message1.Should().NotBe(message2);
        }
    }

    public class PropertyInitialization
    {
        [Fact]
        public void InitializesBodyAsEmpty()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.Body.Should().BeEmpty();
        }

        [Fact]
        public void InitializesExchangeAsEmpty()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.Exchange.Should().BeEmpty();
        }

        [Fact]
        public void InitializesRoutingKeyAsEmpty()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.RoutingKey.Should().BeEmpty();
        }

        [Fact]
        public void InitializesQueueAsEmpty()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.Queue.Should().BeEmpty();
        }

        [Fact]
        public void InitializesDeliveryTagAsZero()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.DeliveryTag.Should().Be(0);
        }

        [Fact]
        public void InitializesRedeliveredAsFalse()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.Redelivered.Should().BeFalse();
        }

        [Fact]
        public void InitializesBodySizeBytesAsZero()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.BodySizeBytes.Should().Be(0);
        }

        [Fact]
        public void InitializesBodySizeAsEmpty()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.BodySize.Should().BeEmpty();
        }

        [Fact]
        public void InitializesPropertiesAsNull()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.Properties.Should().BeNull();
        }

        [Fact]
        public void InitializesHeadersAsNull()
        {
            // Act
            var message = new RetrievedMessage();

            // Assert
            message.Headers.Should().BeNull();
        }
    }

    public class PropertyAssignment
    {
        [Fact]
        public void AllowsSettingBody()
        {
            // Arrange & Act
            var message = new RetrievedMessage { Body = "Test message" };

            // Assert
            message.Body.Should().Be("Test message");
        }

        [Fact]
        public void AllowsSettingProperties()
        {
            // Arrange
            var properties = new MessageProperties { MessageId = "123" };

            // Act
            var message = new RetrievedMessage { Properties = properties };

            // Assert
            message.Properties.Should().BeSameAs(properties);
        }

        [Fact]
        public void AllowsSettingHeaders()
        {
            // Arrange
            var headers = new Dictionary<string, object> { ["x-custom"] = "value" };

            // Act
            var message = new RetrievedMessage { Headers = headers };

            // Assert
            message.Headers.Should().BeSameAs(headers);
        }

        [Fact]
        public void AllowsSettingRoutingMetadata()
        {
            // Arrange & Act
            var message = new RetrievedMessage
            {
                Exchange = "test.exchange",
                RoutingKey = "test.key",
                Queue = "test-queue",
                DeliveryTag = 42,
                Redelivered = true
            };

            // Assert
            message.Exchange.Should().Be("test.exchange");
            message.RoutingKey.Should().Be("test.key");
            message.Queue.Should().Be("test-queue");
            message.DeliveryTag.Should().Be(42);
            message.Redelivered.Should().BeTrue();
        }

        [Fact]
        public void AllowsSettingBodySizeInformation()
        {
            // Arrange & Act
            var message = new RetrievedMessage
            {
                BodySizeBytes = 1024,
                BodySize = "1 KB"
            };

            // Assert
            message.BodySizeBytes.Should().Be(1024);
            message.BodySize.Should().Be("1 KB");
        }
    }

    public class JsonSerialization
    {
        [Fact]
        public void SerializesAllProperties()
        {
            // Arrange
            var message = new RetrievedMessage
            {
                Body = "Test message",
                Exchange = "test.exchange",
                RoutingKey = "test.key",
                Queue = "test-queue",
                DeliveryTag = 42,
                Redelivered = true,
                BodySizeBytes = 12,
                BodySize = "12 bytes",
                Properties = new MessageProperties { MessageId = "msg-123" },
                Headers = new Dictionary<string, object> { ["x-custom"] = "value" }
            };

            // Act
            var json = JsonSerializer.Serialize(message, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("body").GetString().Should().Be("Test message");
            parsed.RootElement.GetProperty("exchange").GetString().Should().Be("test.exchange");
            parsed.RootElement.GetProperty("routingKey").GetString().Should().Be("test.key");
            parsed.RootElement.GetProperty("queue").GetString().Should().Be("test-queue");
            parsed.RootElement.GetProperty("deliveryTag").GetUInt64().Should().Be(42);
            parsed.RootElement.GetProperty("redelivered").GetBoolean().Should().BeTrue();
            parsed.RootElement.GetProperty("bodySizeBytes").GetInt64().Should().Be(12);
            parsed.RootElement.GetProperty("bodySize").GetString().Should().Be("12 bytes");
        }

        [Fact]
        public void IncludesNullProperties()
        {
            // Arrange
            var message = new RetrievedMessage
            {
                Body = "Test",
                Queue = "queue"
            };

            // Act
            var json = JsonSerializer.Serialize(message, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
            var parsed = JsonDocument.Parse(json);

            // Assert
            // Source-generated JSON serialization includes null properties by default
            parsed.RootElement.TryGetProperty("properties", out var props).Should().BeTrue();
            props.ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Fact]
        public void UsesBodyJsonConverter_ForPlainText()
        {
            // Arrange
            var message = new RetrievedMessage { Body = "Plain text message" };

            // Act
            var json = JsonSerializer.Serialize(message, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("body").GetString().Should().Be("Plain text message");
        }

        [Fact]
        public void UsesBodyJsonConverter_ForJsonBody()
        {
            // Arrange - Body that looks like JSON should be parsed as object
            var jsonBody = """{"name": "John", "age": 30}""";
            var message = new RetrievedMessage { Body = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(message, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
            var parsed = JsonDocument.Parse(json);

            // Assert
            var bodyElement = parsed.RootElement.GetProperty("body");
            bodyElement.ValueKind.Should().Be(JsonValueKind.Object);
            bodyElement.GetProperty("name").GetString().Should().Be("John");
            bodyElement.GetProperty("age").GetInt32().Should().Be(30);
        }

        [Fact]
        public void IncludesEmptyStrings()
        {
            // Arrange
            var message = new RetrievedMessage
            {
                Body = "",
                Exchange = "",
                RoutingKey = "",
                Queue = "",
                BodySize = ""
            };

            // Act
            var json = JsonSerializer.Serialize(message, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("body").GetString().Should().BeEmpty();
            parsed.RootElement.GetProperty("exchange").GetString().Should().BeEmpty();
            parsed.RootElement.GetProperty("routingKey").GetString().Should().BeEmpty();
            parsed.RootElement.GetProperty("queue").GetString().Should().BeEmpty();
            parsed.RootElement.GetProperty("bodySize").GetString().Should().BeEmpty();
        }
    }

    public class JsonDeserialization
    {
        [Fact]
        public void DeserializesAllProperties()
        {
            // Arrange
            var json = """
                {
                    "body": "Test message",
                    "exchange": "test.exchange",
                    "routingKey": "test.key",
                    "queue": "test-queue",
                    "deliveryTag": 42,
                    "redelivered": true,
                    "bodySizeBytes": 12,
                    "bodySize": "12 bytes",
                    "properties": {
                        "messageId": "msg-123"
                    },
                    "headers": {
                        "x-custom": "value"
                    }
                }
                """;

            // Act
            var message = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);

            // Assert
            message.Should().NotBeNull();
            message!.Body.Should().Be("Test message");
            message.Exchange.Should().Be("test.exchange");
            message.RoutingKey.Should().Be("test.key");
            message.Queue.Should().Be("test-queue");
            message.DeliveryTag.Should().Be(42);
            message.Redelivered.Should().BeTrue();
            message.BodySizeBytes.Should().Be(12);
            message.BodySize.Should().Be("12 bytes");
            message.Properties.Should().NotBeNull();
            message.Headers.Should().NotBeNull();
        }

        [Fact]
        public void DeserializesWithoutOptionalFields()
        {
            // Arrange
            var json = """
                {
                    "body": "Test",
                    "queue": "queue",
                    "routingKey": "key",
                    "exchange": "",
                    "deliveryTag": 1,
                    "redelivered": false,
                    "bodySizeBytes": 4,
                    "bodySize": "4 bytes"
                }
                """;

            // Act
            var message = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);

            // Assert
            message.Should().NotBeNull();
            message!.Body.Should().Be("Test");
            message.Properties.Should().BeNull();
            message.Headers.Should().BeNull();
        }

        [Fact]
        public void UsesBodyJsonConverter_ForPlainStringInDeserialization()
        {
            // Arrange - Plain string in body field (most common case)
            var json = """
                {
                    "body": "Plain text message",
                    "queue": "queue",
                    "routingKey": "key",
                    "exchange": "",
                    "deliveryTag": 1,
                    "redelivered": false,
                    "bodySizeBytes": 17,
                    "bodySize": "17 bytes"
                }
                """;

            // Act
            var message = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);

            // Assert
            message.Should().NotBeNull();
            message!.Body.Should().Be("Plain text message");
        }
    }

    public class RoundTripSerialization
    {
        [Fact]
        public void RoundTripsCompleteMessage()
        {
            // Arrange
            var original = new RetrievedMessage
            {
                Body = "Test message",
                Exchange = "test.exchange",
                RoutingKey = "test.key",
                Queue = "test-queue",
                DeliveryTag = 42,
                Redelivered = true,
                BodySizeBytes = 12,
                BodySize = "12 bytes",
                Properties = new MessageProperties
                {
                    MessageId = "msg-123",
                    ContentType = "application/json"
                },
                Headers = new Dictionary<string, object>
                {
                    ["x-custom"] = "value"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(original, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Body.Should().Be(original.Body);
            deserialized.Exchange.Should().Be(original.Exchange);
            deserialized.RoutingKey.Should().Be(original.RoutingKey);
            deserialized.Queue.Should().Be(original.Queue);
            deserialized.DeliveryTag.Should().Be(original.DeliveryTag);
            deserialized.Redelivered.Should().Be(original.Redelivered);
            deserialized.BodySizeBytes.Should().Be(original.BodySizeBytes);
            deserialized.BodySize.Should().Be(original.BodySize);
        }

        [Fact]
        public void RoundTripsMinimalMessage()
        {
            // Arrange
            var original = new RetrievedMessage
            {
                Body = "Test",
                Queue = "queue",
                RoutingKey = "key",
                Exchange = "",
                DeliveryTag = 1,
                Redelivered = false,
                BodySizeBytes = 4,
                BodySize = "4 bytes"
            };

            // Act
            var json = JsonSerializer.Serialize(original, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.RetrievedMessage);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Body.Should().Be(original.Body);
            deserialized.Properties.Should().BeNull();
            deserialized.Headers.Should().BeNull();
        }
    }

    public class BodySizeCalculations
    {
        [Theory]
        [InlineData("", 0, "0 bytes")]
        [InlineData("a", 1, "1 bytes")]
        [InlineData("test", 4, "4 bytes")]
        [InlineData("hello world", 11, "11 bytes")]
        public void BodySizeBytes_MatchesUtf8ByteCount(string body, long expectedBytes, string expectedSize)
        {
            // Arrange
            var actualBytes = System.Text.Encoding.UTF8.GetByteCount(body);

            // Act
            var message = new RetrievedMessage
            {
                Body = body,
                BodySizeBytes = actualBytes,
                BodySize = OutputUtilities.ToSizeString(actualBytes)
            };

            // Assert
            message.BodySizeBytes.Should().Be(expectedBytes);
            message.BodySize.Should().Be(expectedSize);
        }

        [Fact]
        public void BodySizeBytes_HandlesUnicodeCharacters()
        {
            // Arrange - Unicode emoji is 4 bytes in UTF-8
            var body = "Hello 👋";
            var actualBytes = System.Text.Encoding.UTF8.GetByteCount(body);

            // Act
            var message = new RetrievedMessage
            {
                Body = body,
                BodySizeBytes = actualBytes,
                BodySize = OutputUtilities.ToSizeString(actualBytes)
            };

            // Assert
            message.BodySizeBytes.Should().Be(10); // "Hello " = 6 bytes, emoji = 4 bytes
        }
    }
}
