using System.Text.Json;
using RmqCli.Core.Models;

namespace RmqCli.Unit.Tests.Core.Models;

public class DestinationInfoTests
{
    #region Property Tests

    public class PropertyTests
    {
        [Fact]
        public void AllowsSettingQueueName()
        {
            // Arrange
            var destination = new DestinationInfo();

            // Act
            destination.Queue = "test-queue";

            // Assert
            destination.Queue.Should().Be("test-queue");
        }

        [Fact]
        public void AllowsSettingRoutingKey()
        {
            // Arrange
            var destination = new DestinationInfo();

            // Act
            destination.RoutingKey = "test-routing-key";

            // Assert
            destination.RoutingKey.Should().Be("test-routing-key");
        }

        [Fact]
        public void AllowsSettingExchange()
        {
            // Arrange
            var destination = new DestinationInfo();

            // Act
            destination.Exchange = "test-exchange";

            // Assert
            destination.Exchange.Should().Be("test-exchange");
        }

        [Fact]
        public void AllowsSettingType()
        {
            // Arrange
            var destination = new DestinationInfo();

            // Act
            destination.Type = "queue";

            // Assert
            destination.Type.Should().Be("queue");
        }

        [Fact]
        public void InitializesTypeAsEmpty()
        {
            // Act
            var destination = new DestinationInfo();

            // Assert
            destination.Type.Should().BeEmpty();
        }

        [Fact]
        public void InitializesQueueAsNull()
        {
            // Act
            var destination = new DestinationInfo();

            // Assert
            destination.Queue.Should().BeNull();
        }

        [Fact]
        public void InitializesRoutingKeyAsNull()
        {
            // Act
            var destination = new DestinationInfo();

            // Assert
            destination.RoutingKey.Should().BeNull();
        }

        [Fact]
        public void InitializesExchangeAsNull()
        {
            // Act
            var destination = new DestinationInfo();

            // Assert
            destination.Exchange.Should().BeNull();
        }
    }

    #endregion

    #region JSON Serialization

    public class JsonSerialization
    {
        [Fact]
        public void SerializesQueueDestination()
        {
            // Arrange
            var destination = new DestinationInfo
            {
                Type = "queue",
                Queue = "test-queue"
            };

            // Act
            var json = JsonSerializer.Serialize(destination);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("queue", out var queue).Should().BeTrue();
            queue.GetString().Should().Be("test-queue");
            parsed.RootElement.TryGetProperty("type", out _).Should().BeFalse(); // Type is JsonIgnore
        }

        [Fact]
        public void SerializesRoutingKeyDestination()
        {
            // Arrange
            var destination = new DestinationInfo
            {
                Type = "exchange",
                RoutingKey = "test-key",
                Exchange = "test-exchange"
            };

            // Act
            var json = JsonSerializer.Serialize(destination);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("routing_key", out var routingKey).Should().BeTrue();
            routingKey.GetString().Should().Be("test-key");
            parsed.RootElement.TryGetProperty("exchange", out var exchange).Should().BeTrue();
            exchange.GetString().Should().Be("test-exchange");
            parsed.RootElement.TryGetProperty("type", out _).Should().BeFalse(); // Type is JsonIgnore
        }

        [Fact]
        public void OmitsNullQueue_WhenNotSet()
        {
            // Arrange
            var destination = new DestinationInfo
            {
                RoutingKey = "test-key"
            };

            // Act
            var json = JsonSerializer.Serialize(destination);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("queue", out _).Should().BeFalse();
        }

        [Fact]
        public void OmitsNullRoutingKey_WhenNotSet()
        {
            // Arrange
            var destination = new DestinationInfo
            {
                Queue = "test-queue"
            };

            // Act
            var json = JsonSerializer.Serialize(destination);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("routing_key", out _).Should().BeFalse();
        }

        [Fact]
        public void OmitsNullExchange_WhenNotSet()
        {
            // Arrange
            var destination = new DestinationInfo
            {
                Queue = "test-queue"
            };

            // Act
            var json = JsonSerializer.Serialize(destination);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("exchange", out _).Should().BeFalse();
        }

        [Fact]
        public void UsesSnakeCase_ForPropertyNames()
        {
            // Arrange
            var destination = new DestinationInfo
            {
                RoutingKey = "key",
                Exchange = "exchange"
            };

            // Act
            var json = JsonSerializer.Serialize(destination);

            // Assert
            json.Should().Contain("\"routing_key\"");
            json.Should().Contain("\"exchange\"");
            json.Should().NotContain("\"RoutingKey\"");
            json.Should().NotContain("\"Exchange\"");
        }

        [Fact]
        public void SerializesEmptyDestination()
        {
            // Arrange
            var destination = new DestinationInfo();

            // Act
            var json = JsonSerializer.Serialize(destination);
            var parsed = JsonDocument.Parse(json);

            // Assert
            // Should serialize to empty object {} since all nullable fields are omitted
            parsed.RootElement.EnumerateObject().Should().BeEmpty();
        }

        [Fact]
        public void AlwaysOmitsType_DueToJsonIgnore()
        {
            // Arrange
            var destinations = new[]
            {
                new DestinationInfo { Type = "queue", Queue = "q" },
                new DestinationInfo { Type = "exchange", RoutingKey = "rk" },
                new DestinationInfo { Type = "anything" }
            };

            // Act & Assert
            foreach (var destination in destinations)
            {
                var json = JsonSerializer.Serialize(destination);
                var parsed = JsonDocument.Parse(json);
                parsed.RootElement.TryGetProperty("type", out _).Should().BeFalse();
            }
        }
    }

    #endregion

    #region JSON Deserialization

    public class JsonDeserialization
    {
        [Fact]
        public void DeserializesQueueDestination()
        {
            // Arrange
            var json = "{\"queue\":\"test-queue\"}";

            // Act
            var destination = JsonSerializer.Deserialize<DestinationInfo>(json);

            // Assert
            destination.Should().NotBeNull();
            destination!.Queue.Should().Be("test-queue");
            destination.RoutingKey.Should().BeNull();
            destination.Exchange.Should().BeNull();
        }

        [Fact]
        public void DeserializesRoutingKeyDestination()
        {
            // Arrange
            var json = "{\"routing_key\":\"test-key\",\"exchange\":\"test-exchange\"}";

            // Act
            var destination = JsonSerializer.Deserialize<DestinationInfo>(json);

            // Assert
            destination.Should().NotBeNull();
            destination!.RoutingKey.Should().Be("test-key");
            destination.Exchange.Should().Be("test-exchange");
            destination.Queue.Should().BeNull();
        }

        [Fact]
        public void DeserializesEmptyObject()
        {
            // Arrange
            var json = "{}";

            // Act
            var destination = JsonSerializer.Deserialize<DestinationInfo>(json);

            // Assert
            destination.Should().NotBeNull();
            destination!.Queue.Should().BeNull();
            destination.RoutingKey.Should().BeNull();
            destination.Exchange.Should().BeNull();
        }

        [Fact]
        public void IgnoresTypeProperty_InJson()
        {
            // Arrange - Type property in JSON should be ignored due to JsonIgnore
            var json = "{\"type\":\"queue\",\"queue\":\"test-queue\"}";

            // Act
            var destination = JsonSerializer.Deserialize<DestinationInfo>(json);

            // Assert
            destination.Should().NotBeNull();
            destination!.Type.Should().BeEmpty(); // Type is not deserialized
            destination.Queue.Should().Be("test-queue");
        }

        [Fact]
        public void HandlesExtraProperties_Gracefully()
        {
            // Arrange
            var json = "{\"queue\":\"test\",\"unknown\":\"value\"}";

            // Act
            var destination = JsonSerializer.Deserialize<DestinationInfo>(json);

            // Assert
            destination.Should().NotBeNull();
            destination!.Queue.Should().Be("test");
        }
    }

    #endregion

    #region Round-trip Tests

    public class RoundTripTests
    {
        [Fact]
        public void RoundTripsQueueDestination()
        {
            // Arrange
            var original = new DestinationInfo
            {
                Queue = "test-queue"
            };

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<DestinationInfo>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Queue.Should().Be(original.Queue);
        }

        [Fact]
        public void RoundTripsRoutingKeyDestination()
        {
            // Arrange
            var original = new DestinationInfo
            {
                RoutingKey = "test-key",
                Exchange = "test-exchange"
            };

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<DestinationInfo>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.RoutingKey.Should().Be(original.RoutingKey);
            deserialized.Exchange.Should().Be(original.Exchange);
        }

        [Fact]
        public void TypeIsNotRoundTripped()
        {
            // Arrange
            var original = new DestinationInfo
            {
                Type = "queue",
                Queue = "test-queue"
            };

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<DestinationInfo>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Type.Should().BeEmpty(); // Type is not serialized/deserialized
            deserialized.Queue.Should().Be(original.Queue);
        }
    }

    #endregion
}
