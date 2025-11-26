using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Unit.Tests.Core.Models;

public class MessagePropertiesTests
{
    public class RecordBehavior
    {
        [Fact]
        public void IsRecordType()
        {
            // Arrange & Act
            var props = new MessageProperties();

            // Assert
            props.Should().BeAssignableTo<MessageProperties>();
        }

        [Fact]
        public void SupportsWithExpression()
        {
            // Arrange
            var original = new MessageProperties
            {
                MessageId = "msg-1",
                ContentType = "application/json"
            };

            // Act
            var modified = original with { MessageId = "msg-2" };

            // Assert
            modified.MessageId.Should().Be("msg-2");
            modified.ContentType.Should().Be("application/json");
            original.MessageId.Should().Be("msg-1");
        }

        [Fact]
        public void Equality_ComparesAllProperties()
        {
            // Arrange
            var props1 = new MessageProperties
            {
                MessageId = "msg-1",
                ContentType = "text/plain",
                DeliveryMode = DeliveryModes.Persistent
            };

            var props2 = new MessageProperties
            {
                MessageId = "msg-1",
                ContentType = "text/plain",
                DeliveryMode = DeliveryModes.Persistent
            };

            // Act & Assert
            props1.Should().Be(props2);
            (props1 == props2).Should().BeTrue();
        }

        [Fact]
        public void Inequality_WhenPropertiesDiffer()
        {
            // Arrange
            var props1 = new MessageProperties { MessageId = "msg-1" };
            var props2 = new MessageProperties { MessageId = "msg-2" };

            // Act & Assert
            props1.Should().NotBe(props2);
        }
    }

    public class PropertyInitialization
    {
        [Fact]
        public void InitializesAllPropertiesAsNull()
        {
            // Act
            var props = new MessageProperties();

            // Assert
            props.AppId.Should().BeNull();
            props.ClusterId.Should().BeNull();
            props.ContentType.Should().BeNull();
            props.ContentEncoding.Should().BeNull();
            props.CorrelationId.Should().BeNull();
            props.DeliveryMode.Should().BeNull();
            props.Expiration.Should().BeNull();
            props.MessageId.Should().BeNull();
            props.Priority.Should().BeNull();
            props.ReplyTo.Should().BeNull();
            props.Timestamp.Should().BeNull();
            props.Type.Should().BeNull();
            props.UserId.Should().BeNull();
        }
    }

    public class PropertyAssignment
    {
        [Fact]
        public void AllowsSettingAppId()
        {
            // Arrange & Act
            var props = new MessageProperties { AppId = "my-app" };

            // Assert
            props.AppId.Should().Be("my-app");
        }

        [Fact]
        public void AllowsSettingClusterId()
        {
            // Arrange & Act
            var props = new MessageProperties { ClusterId = "cluster-1" };

            // Assert
            props.ClusterId.Should().Be("cluster-1");
        }

        [Fact]
        public void AllowsSettingContentType()
        {
            // Arrange & Act
            var props = new MessageProperties { ContentType = "application/json" };

            // Assert
            props.ContentType.Should().Be("application/json");
        }

        [Fact]
        public void AllowsSettingContentEncoding()
        {
            // Arrange & Act
            var props = new MessageProperties { ContentEncoding = "utf-8" };

            // Assert
            props.ContentEncoding.Should().Be("utf-8");
        }

        [Fact]
        public void AllowsSettingCorrelationId()
        {
            // Arrange & Act
            var props = new MessageProperties { CorrelationId = "corr-123" };

            // Assert
            props.CorrelationId.Should().Be("corr-123");
        }

        [Fact]
        public void AllowsSettingDeliveryMode_Transient()
        {
            // Arrange & Act
            var props = new MessageProperties { DeliveryMode = DeliveryModes.Transient };

            // Assert
            props.DeliveryMode.Should().Be(DeliveryModes.Transient);
        }

        [Fact]
        public void AllowsSettingDeliveryMode_Persistent()
        {
            // Arrange & Act
            var props = new MessageProperties { DeliveryMode = DeliveryModes.Persistent };

            // Assert
            props.DeliveryMode.Should().Be(DeliveryModes.Persistent);
        }

        [Fact]
        public void AllowsSettingExpiration()
        {
            // Arrange & Act
            var props = new MessageProperties { Expiration = "60000" };

            // Assert
            props.Expiration.Should().Be("60000");
        }

        [Fact]
        public void AllowsSettingMessageId()
        {
            // Arrange & Act
            var props = new MessageProperties { MessageId = "msg-001" };

            // Assert
            props.MessageId.Should().Be("msg-001");
        }

        [Fact]
        public void AllowsSettingPriority()
        {
            // Arrange & Act
            var props = new MessageProperties { Priority = 5 };

            // Assert
            props.Priority.Should().Be(5);
        }

        [Fact]
        public void AllowsSettingReplyTo()
        {
            // Arrange & Act
            var props = new MessageProperties { ReplyTo = "reply-queue" };

            // Assert
            props.ReplyTo.Should().Be("reply-queue");
        }

        [Fact]
        public void AllowsSettingTimestamp()
        {
            // Arrange
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Act
            var props = new MessageProperties { Timestamp = timestamp };

            // Assert
            props.Timestamp.Should().Be(timestamp);
        }

        [Fact]
        public void AllowsSettingType()
        {
            // Arrange & Act
            var props = new MessageProperties { Type = "user.created" };

            // Assert
            props.Type.Should().Be("user.created");
        }

        [Fact]
        public void AllowsSettingUserId()
        {
            // Arrange & Act
            var props = new MessageProperties { UserId = "guest" };

            // Assert
            props.UserId.Should().Be("guest");
        }
    }

    public class HasAnyPropertyMethod
    {
        [Fact]
        public void ReturnsFalse_WhenAllPropertiesAreNull()
        {
            // Arrange
            var props = new MessageProperties();

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ReturnsTrue_WhenTypeIsSet()
        {
            // Arrange
            var props = new MessageProperties { Type = "test.type" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenMessageIdIsSet()
        {
            // Arrange
            var props = new MessageProperties { MessageId = "msg-1" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenAppIdIsSet()
        {
            // Arrange
            var props = new MessageProperties { AppId = "my-app" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenClusterIdIsSet()
        {
            // Arrange
            var props = new MessageProperties { ClusterId = "cluster-1" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenContentTypeIsSet()
        {
            // Arrange
            var props = new MessageProperties { ContentType = "application/json" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenContentEncodingIsSet()
        {
            // Arrange
            var props = new MessageProperties { ContentEncoding = "utf-8" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenCorrelationIdIsSet()
        {
            // Arrange
            var props = new MessageProperties { CorrelationId = "corr-123" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenDeliveryModeIsSet()
        {
            // Arrange
            var props = new MessageProperties { DeliveryMode = DeliveryModes.Persistent };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenExpirationIsSet()
        {
            // Arrange
            var props = new MessageProperties { Expiration = "60000" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenPriorityIsSet()
        {
            // Arrange
            var props = new MessageProperties { Priority = 5 };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenReplyToIsSet()
        {
            // Arrange
            var props = new MessageProperties { ReplyTo = "reply-queue" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenTimestampIsSet()
        {
            // Arrange
            var props = new MessageProperties { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenUserIdIsSet()
        {
            // Arrange
            var props = new MessageProperties { UserId = "guest" };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenMultiplePropertiesAreSet()
        {
            // Arrange
            var props = new MessageProperties
            {
                MessageId = "msg-1",
                ContentType = "application/json",
                Priority = 5
            };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ReturnsTrue_WhenAllPropertiesAreSet()
        {
            // Arrange
            var props = new MessageProperties
            {
                Type = "test.type",
                MessageId = "msg-1",
                AppId = "my-app",
                ClusterId = "cluster-1",
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                CorrelationId = "corr-123",
                DeliveryMode = DeliveryModes.Persistent,
                Expiration = "60000",
                Priority = 5,
                ReplyTo = "reply-queue",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UserId = "guest"
            };

            // Act
            var result = props.HasAnyProperty();

            // Assert
            result.Should().BeTrue();
        }
    }

    public class TimestampHandling
    {
        [Fact]
        public void Timestamp_StoresUnixSeconds()
        {
            // Arrange
            var dateTime = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.Zero);
            var expectedSeconds = dateTime.ToUnixTimeSeconds();

            // Act
            var props = new MessageProperties { Timestamp = expectedSeconds };

            // Assert
            props.Timestamp.Should().Be(expectedSeconds);
        }

        [Fact]
        public void Timestamp_CanRoundTripFromDateTime()
        {
            // Arrange
            var originalDateTime = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
            var unixSeconds = originalDateTime.ToUnixTimeSeconds();

            // Act
            var props = new MessageProperties { Timestamp = unixSeconds };
            var reconstructedDateTime = DateTimeOffset.FromUnixTimeSeconds(props.Timestamp!.Value);

            // Assert
            reconstructedDateTime.Should().Be(originalDateTime);
        }

        [Fact]
        public void Timestamp_SupportsZeroValue()
        {
            // Arrange & Act
            var props = new MessageProperties { Timestamp = 0 };

            // Assert
            props.Timestamp.Should().Be(0);
            DateTimeOffset.FromUnixTimeSeconds(0).Year.Should().Be(1970);
        }
    }

    public class PriorityHandling
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(255)]
        public void Priority_SupportsValidRanges(byte priority)
        {
            // Arrange & Act
            var props = new MessageProperties { Priority = priority };

            // Assert
            props.Priority.Should().Be(priority);
        }

        [Fact]
        public void Priority_CanBeNull()
        {
            // Arrange & Act
            var props = new MessageProperties { Priority = null };

            // Assert
            props.Priority.Should().BeNull();
        }
    }

    public class DeliveryModeHandling
    {
        [Fact]
        public void DeliveryMode_SupportsTransient()
        {
            // Arrange & Act
            var props = new MessageProperties { DeliveryMode = DeliveryModes.Transient };

            // Assert
            props.DeliveryMode.Should().Be(DeliveryModes.Transient);
            ((int)props.DeliveryMode.Value).Should().Be(1);
        }

        [Fact]
        public void DeliveryMode_SupportsPersistent()
        {
            // Arrange & Act
            var props = new MessageProperties { DeliveryMode = DeliveryModes.Persistent };

            // Assert
            props.DeliveryMode.Should().Be(DeliveryModes.Persistent);
            ((int)props.DeliveryMode.Value).Should().Be(2);
        }

        [Fact]
        public void DeliveryMode_CanBeNull()
        {
            // Arrange & Act
            var props = new MessageProperties { DeliveryMode = null };

            // Assert
            props.DeliveryMode.Should().BeNull();
        }
    }
}
