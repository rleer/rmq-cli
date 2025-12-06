using System.Text;
using RabbitMQ.Client;
using RmqCli.Shared.Output.Formatters;

namespace RmqCli.Unit.Tests.Shared.Output.Formatters;

public class MessagePropertyExtractorTests
{
    public class ExtractProperties
    {
        [Fact]
        public void ReturnsEmptyProperties_WhenPropsIsNull()
        {
            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(null);

            // Assert
            result.Should().NotBeNull();
            result.HasAnyProperty().Should().BeFalse();
        }

        [Fact]
        public void ExtractsAppId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsAppIdPresent().Returns(true);
            props.AppId.Returns("my-app");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.AppId.Should().Be("my-app");
        }

        [Fact]
        public void ExtractsClusterId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsClusterIdPresent().Returns(true);
            props.ClusterId.Returns("cluster-1");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.ClusterId.Should().Be("cluster-1");
        }

        [Fact]
        public void ExtractsContentType_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsContentTypePresent().Returns(true);
            props.ContentType.Returns("application/json");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.ContentType.Should().Be("application/json");
        }

        [Fact]
        public void ExtractsContentEncoding_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsContentEncodingPresent().Returns(true);
            props.ContentEncoding.Returns("gzip");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.ContentEncoding.Should().Be("gzip");
        }

        [Fact]
        public void ExtractsCorrelationId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsCorrelationIdPresent().Returns(true);
            props.CorrelationId.Returns("corr-123");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.CorrelationId.Should().Be("corr-123");
        }

        [Fact]
        public void ExtractsDeliveryMode_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsDeliveryModePresent().Returns(true);
            props.DeliveryMode.Returns(DeliveryModes.Persistent);

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.DeliveryMode.Should().Be(DeliveryModes.Persistent);
        }

        [Fact]
        public void ExtractsExpiration_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsExpirationPresent().Returns(true);
            props.Expiration.Returns("60000");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.Expiration.Should().Be("60000");
        }
        
        [Fact]
        public void ExtractsMessageId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-12345");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.MessageId.Should().Be("msg-12345");
        }

        [Fact]
        public void ExtractsPriority_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsPriorityPresent().Returns(true);
            props.Priority.Returns((byte)5);

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.Priority.Should().Be(5);
        }

        [Fact]
        public void ExtractsReplyTo_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsReplyToPresent().Returns(true);
            props.ReplyTo.Returns("reply-queue");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.ReplyTo.Should().Be("reply-queue");
        }

        [Fact]
        public void ExtractsAndFormatsTimestamp_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsTimestampPresent().Returns(true);
            props.Timestamp.Returns(new AmqpTimestamp(1609459200)); // 2021-01-01 00:00:00 UTC

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.Timestamp.Should().Be(1609459200L);
        }
       
        [Fact]
        public void ExtractsType_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsTypePresent().Returns(true);
            props.Type.Returns("application.event");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.Type.Should().Be("application.event");
        }
        
        [Fact]
        public void ExtractsUserId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsUserIdPresent().Returns(true);
            props.UserId.Returns("user-123");

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.UserId.Should().Be("user-123");
        }

        [Fact]
        public void OmitsProperties_WhenNotPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsTypePresent().Returns(false);
            props.IsMessageIdPresent().Returns(false);
            props.IsAppIdPresent().Returns(false);

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.Type.Should().BeNull();
            result.MessageId.Should().BeNull();
            result.AppId.Should().BeNull();
        }

        [Fact]
        public void ExtractsAllProperties_WhenAllPresent()
        {
            // Arrange
            var props = CreateFullyPopulatedProperties();

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.AppId.Should().Be("test-app");
            result.ClusterId.Should().Be("cluster-1");
            result.ContentType.Should().Be("application/json");
            result.ContentEncoding.Should().Be("utf-8");
            result.CorrelationId.Should().Be("corr-123");
            result.DeliveryMode.Should().Be(DeliveryModes.Persistent);
            result.Expiration.Should().Be("60000");
            result.MessageId.Should().Be("msg-001");
            result.Priority.Should().Be(5);
            result.ReplyTo.Should().Be("reply-queue");
            result.Timestamp.Should().NotBeNull();
            result.Type.Should().Be("test.type");
            result.UserId.Should().NotBeNull();
        }
    }

    public class HeaderConversion
    {
        [Fact]
        public void ReturnsNull_WhenHeadersNotPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(false);

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().BeNull();
        }

        [Fact]
        public void ReturnsNull_WhenHeadersAreEmpty()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>());

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().BeNull();
        }

        [Fact]
        public void ConvertsStringHeaders_Correctly()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-string"] = "test-value",
                ["x-another"] = "another-value"
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            headers["x-string"].Should().Be("test-value");
            headers["x-another"].Should().Be("another-value");
        }

        [Fact]
        public void ConvertsNumericHeaders_Correctly()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-int"] = 42,
                ["x-long"] = 123456789L,
                ["x-double"] = 3.14
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            headers["x-int"].Should().Be(42);
            headers["x-long"].Should().Be(123456789L);
            headers["x-double"].Should().Be(3.14);
        }

        [Fact]
        public void ConvertsUtf8ByteArray_ToString()
        {
            // Arrange
            var utf8Bytes = "Hello, World!"u8.ToArray();
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-bytes"] = utf8Bytes
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            headers["x-bytes"].Should().Be("Hello, World!");
        }

        [Fact]
        public void ConvertsBinaryByteArray_ToDescription()
        {
            // Arrange - binary data with null bytes (control characters)
            var binaryBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF };
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-binary"] = binaryBytes
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            headers["x-binary"].Should().Be("<binary data: 5 bytes>");
        }

        [Fact]
        public void ConvertsTimestamp_ToFormattedString()
        {
            // Arrange
            var timestamp = new AmqpTimestamp(1609459200); // 2021-01-01 00:00:00
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-timestamp"] = timestamp
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            headers!["x-timestamp"].Should().Be(1609459200L);
        }

        [Fact]
        public void ConvertsArrays_Recursively()
        {
            // Arrange
            var array = new object[] { "string", 42, "test"u8.ToArray() };
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-array"] = array
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            var convertedArray = headers!["x-array"] as object[];
            convertedArray.Should().NotBeNull();
            convertedArray.Should().HaveCount(3);
            convertedArray[0].Should().Be("string");
            convertedArray[1].Should().Be(42);
            convertedArray[2].Should().Be("test");
        }

        [Fact]
        public void ConvertsNestedDictionaries_Recursively()
        {
            // Arrange
            var nestedDict = new Dictionary<string, object>
            {
                ["nested-key"] = "nested-value",
                ["nested-number"] = 99
            };
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-nested"] = nestedDict
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            var convertedDict = headers["x-nested"] as Dictionary<string, object>;
            convertedDict.Should().NotBeNull();
            convertedDict["nested-key"].Should().Be("nested-value");
            convertedDict["nested-number"].Should().Be(99);
        }

        [Fact]
        public void SkipsNullHeaderValues()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-null"] = null,
                ["x-valid"] = "valid-value"
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            headers.Should().ContainKey("x-valid");
            headers.Should().NotContainKey("x-null");
        }

        [Fact]
        public void HandlesCommonWhitespaceCharacters_InByteArrays()
        {
            // Arrange - string with newline, tab, carriage return
            var textWithWhitespace = "Line1\r\nLine2\tTabbed";
            var bytes = Encoding.UTF8.GetBytes(textWithWhitespace);
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-text"] = bytes
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            headers["x-text"].Should().Be(textWithWhitespace);
        }
    }

    public class EdgeCases
    {
        [Fact]
        public void HandlesEmptyByteArray()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-empty"] = Array.Empty<byte>()
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            headers["x-empty"].Should().Be(string.Empty);
        }

        [Fact]
        public void HandlesEmptyArray()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-array"] = Array.Empty<object>()
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            var array = headers["x-array"] as object[];
            array.Should().NotBeNull();
            array.Should().BeEmpty();
        }

        [Fact]
        public void HandlesComplexNestedStructure()
        {
            // Arrange
            var complex = new Dictionary<string, object>
            {
                ["level1-string"] = "value",
                ["level1-array"] = new object[]
                {
                    "string-in-array",
                    new Dictionary<string, object>
                    {
                        ["level2-nested"] = "deeply-nested-value"
                    }
                }
            };

            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-complex"] = complex
            });

            // Act
            var (_, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            headers.Should().NotBeNull();
            var converted = headers["x-complex"] as Dictionary<string, object>;
            converted.Should().NotBeNull();
            converted["level1-string"].Should().Be("value");

            var array = converted["level1-array"] as object[];
            array.Should().NotBeNull();
            array[0].Should().Be("string-in-array");

            var nestedDict = array[1] as Dictionary<string, object>;
            nestedDict.Should().NotBeNull();
            nestedDict["level2-nested"].Should().Be("deeply-nested-value");
        }

        [Fact]
        public void FormatsTimestamp_AtEpoch()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsTimestampPresent().Returns(true);
            props.Timestamp.Returns(new AmqpTimestamp(0)); // Unix epoch

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.Timestamp.Should().Be(0L);
        }

        [Fact]
        public void FormatsTimestamp_InFuture()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsTimestampPresent().Returns(true);
            props.Timestamp.Returns(new AmqpTimestamp(2000000000)); // 2033-05-18

            // Act
            var (result, _) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);

            // Assert
            result.Timestamp.Should().Be(2000000000L);
        }
    }

    #region Test Helpers

    private static IReadOnlyBasicProperties CreateFullyPopulatedProperties()
    {
        var props = Substitute.For<IReadOnlyBasicProperties>();

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

        props.IsMessageIdPresent().Returns(true);
        props.MessageId.Returns("msg-001");
        
        props.IsPriorityPresent().Returns(true);
        props.Priority.Returns((byte)5);

        props.IsReplyToPresent().Returns(true);
        props.ReplyTo.Returns("reply-queue");

        props.IsTimestampPresent().Returns(true);
        props.Timestamp.Returns(new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

        props.IsTypePresent().Returns(true);
        props.Type.Returns("test.type");
        
        props.IsUserIdPresent().Returns(true);
        props.UserId.Returns("user-123");

        return props;
    }

    #endregion
}
