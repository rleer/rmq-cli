using RabbitMQ.Client;
using RmqCli.Commands.Publish;

namespace RmqCli.Unit.Tests.Commands.Publish;

public class JsonMessageParserTests
{
    public class ParseSingle
    {
        [Fact]
        public void ParsesSimpleMessage_WithBodyOnly()
        {
            // Arrange
            var json = """{"body": "Hello World"}""";

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Should().NotBeNull();
            message.Body.Should().Be("Hello World");
            message.Properties.Should().BeNull();
            message.Headers.Should().BeNull();
        }

        [Fact]
        public void ParsesMessage_WithBodyAndProperties()
        {
            // Arrange
            var json = """
                {
                    "body": "Test message",
                    "properties": {
                        "contentType": "application/json",
                        "correlationId": "123-456",
                        "deliveryMode": 2
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Body.Should().Be("Test message");
            message.Properties.Should().NotBeNull();
            message.Properties.ContentType.Should().Be("application/json");
            message.Properties.CorrelationId.Should().Be("123-456");
            message.Properties.DeliveryMode.Should().Be(DeliveryModes.Persistent);
        }

        [Fact]
        public void ParsesMessage_WithBodyAndHeaders()
        {
            // Arrange
            var json = """
                {
                    "body": "Test message",
                    "headers": {
                        "x-custom-header": "value",
                        "x-priority": 5
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Body.Should().Be("Test message");
            message.Headers.Should().NotBeNull();
            message.Headers.Should().HaveCount(2);
            message.Headers["x-custom-header"].Should().Be("value");
            message.Headers["x-priority"].Should().Be(5);
        }

        [Fact]
        public void ParsesMessage_WithAllProperties()
        {
            // Arrange
            var json = """
                {
                    "body": "Complete message",
                    "properties": {
                        "appId": "test-app",
                        "contentType": "text/plain",
                        "contentEncoding": "utf-8",
                        "correlationId": "corr-123",
                        "deliveryMode": 2,
                        "expiration": "60000",
                        "priority": 5,
                        "replyTo": "reply-queue",
                        "type": "test-type",
                        "userId": "test-user",
                        "timestamp": 1625077765,
                        "clusterId": "cluster-1"
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Body.Should().Be("Complete message");
            var props = message.Properties;
            props.Should().NotBeNull();
            props.AppId.Should().Be("test-app");
            props.ContentType.Should().Be("text/plain");
            props.ContentEncoding.Should().Be("utf-8");
            props.CorrelationId.Should().Be("corr-123");
            props.DeliveryMode.Should().Be(DeliveryModes.Persistent);
            props.Expiration.Should().Be("60000");
            props.Priority.Should().Be(5);
            props.ReplyTo.Should().Be("reply-queue");
            props.Type.Should().Be("test-type");
            props.UserId.Should().Be("test-user");
            props.Timestamp.Should().Be(1625077765);
            props.ClusterId.Should().Be("cluster-1");
        }

        [Fact]
        public void NormalizesHeaderValues_WithStringType()
        {
            // Arrange
            var json = """
                {
                    "body": "test",
                    "headers": {
                        "stringHeader": "text value"
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Headers.Should().NotBeNull();
            message.Headers["stringHeader"].Should().BeOfType<string>();
            message.Headers["stringHeader"].Should().Be("text value");
        }

        [Fact]
        public void NormalizesHeaderValues_WithIntegerType()
        {
            // Arrange
            var json = """
                {
                    "body": "test",
                    "headers": {
                        "intHeader": 42
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Headers.Should().NotBeNull();
            // System.Text.Json deserializes numbers as double when using Dictionary<string, object>
            message.Headers["intHeader"].Should().BeOfType<double>();
            message.Headers["intHeader"].Should().Be(42.0);
        }

        [Fact]
        public void NormalizesHeaderValues_WithDoubleType()
        {
            // Arrange
            var json = """
                {
                    "body": "test",
                    "headers": {
                        "doubleHeader": 3.14
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Headers.Should().NotBeNull();
            message.Headers["doubleHeader"].Should().BeOfType<double>();
            message.Headers["doubleHeader"].Should().Be(3.14);
        }

        [Fact]
        public void NormalizesHeaderValues_WithBooleanType()
        {
            // Arrange
            var json = """
                {
                    "body": "test",
                    "headers": {
                        "boolHeader": true
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Headers.Should().NotBeNull();
            message.Headers["boolHeader"].Should().BeOfType<bool>();
            message.Headers["boolHeader"].Should().Be(true);
        }

        [Fact]
        public void NormalizesHeaderValues_WithNullType()
        {
            // Arrange
            var json = """
                {
                    "body": "test",
                    "headers": {
                        "nullHeader": null
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            // Null values in JSON are deserialized as null (not JsonElement)
            message.Headers.Should().NotBeNull();
            message.Headers.Should().ContainKey("nullHeader");
            message.Headers["nullHeader"].Should().BeNull();
        }

        [Fact]
        public void NormalizesHeaderValues_WithMixedTypes()
        {
            // Arrange
            var json = """
                {
                    "body": "test",
                    "headers": {
                        "string": "text",
                        "int": 100,
                        "double": 2.5,
                        "bool": false,
                        "null": null
                    }
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Headers.Should().NotBeNull();
            message.Headers.Should().HaveCount(5);
            message.Headers["string"].Should().Be("text");
            // System.Text.Json deserializes all numbers as double
            message.Headers["int"].Should().Be(100.0);
            message.Headers["double"].Should().Be(2.5);
            message.Headers["bool"].Should().Be(false);
            message.Headers["null"].Should().BeNull();
        }

        [Fact]
        public void ThrowsArgumentException_WhenJsonIsInvalid()
        {
            // Arrange
            var invalidJson = """{"body": "test", invalid""";

            // Act
            var act = () => JsonMessageParser.ParseSingle(invalidJson);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Invalid JSON message format:*");
        }

        [Fact]
        public void ThrowsArgumentException_WhenResultIsNull()
        {
            // Arrange
            var json = "null";

            // Act
            var act = () => JsonMessageParser.ParseSingle(json);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Failed to parse JSON message: result was null");
        }

        [Fact]
        public void ParsesMessage_WithEmptyProperties()
        {
            // Arrange
            var json = """
                {
                    "body": "test",
                    "properties": {}
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Body.Should().Be("test");
            message.Properties.Should().NotBeNull();
        }

        [Fact]
        public void ParsesMessage_WithEmptyHeaders()
        {
            // Arrange
            var json = """
                {
                    "body": "test",
                    "headers": {}
                }
                """;

            // Act
            var message = JsonMessageParser.ParseSingle(json);

            // Assert
            message.Body.Should().Be("test");
            message.Headers.Should().NotBeNull();
            message.Headers.Should().BeEmpty();
        }
    }

    public class ParseNdjson
    {
        [Fact]
        public void ParsesMultipleMessages_SeparatedByNewlines()
        {
            // Arrange
            var ndjson = """
                {"body": "Message 1"}
                {"body": "Message 2"}
                {"body": "Message 3"}
                """;

            // Act
            var messages = JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            messages.Should().HaveCount(3);
            messages[0].Body.Should().Be("Message 1");
            messages[1].Body.Should().Be("Message 2");
            messages[2].Body.Should().Be("Message 3");
        }

        [Fact]
        public void ParsesMessages_WithPropertiesAndHeaders()
        {
            // Arrange
            var ndjson = """
                {"body": "Msg 1", "properties": {"contentType": "text/plain"}}
                {"body": "Msg 2", "headers": {"x-custom": "value"}}
                """;

            // Act
            var messages = JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            messages.Should().HaveCount(2);
            messages[0].Properties!.ContentType.Should().Be("text/plain");
            messages[1].Headers!["x-custom"].Should().Be("value");
        }

        [Fact]
        public void SkipsEmptyLines()
        {
            // Arrange
            var ndjson = """
                {"body": "Message 1"}

                {"body": "Message 2"}


                {"body": "Message 3"}
                """;

            // Act
            var messages = JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            messages.Should().HaveCount(3);
        }

        [Fact]
        public void SkipsWhitespaceOnlyLines()
        {
            // Arrange
            var ndjson = """
                {"body": "Message 1"}

                {"body": "Message 2"}

                {"body": "Message 3"}
                """;

            // Act
            var messages = JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            messages.Should().HaveCount(3);
        }

        [Fact]
        public void ThrowsArgumentException_WhenLineIsInvalid()
        {
            // Arrange
            var ndjson = """
                {"body": "Message 1"}
                {"body": "Message 2", invalid
                {"body": "Message 3"}
                """;

            // Act
            var act = () => JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Failed to parse line 2:*");
        }

        [Fact]
        public void ReturnsEmptyList_WhenInputIsEmpty()
        {
            // Arrange
            var ndjson = "";

            // Act
            var messages = JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            messages.Should().BeEmpty();
        }

        [Fact]
        public void ReturnsEmptyList_WhenInputIsWhitespaceOnly()
        {
            // Arrange
            var ndjson = "   \n\n  \t  \n  ";

            // Act
            var messages = JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            messages.Should().BeEmpty();
        }

        [Fact]
        public void ParsesSingleMessage_WithoutTrailingNewline()
        {
            // Arrange
            var ndjson = """{"body": "Single message"}""";

            // Act
            var messages = JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            messages.Should().HaveCount(1);
            messages[0].Body.Should().Be("Single message");
        }

        [Fact]
        public void NormalizesHeaderValues_InAllMessages()
        {
            // Arrange
            var ndjson = """
                {"body": "Msg 1", "headers": {"count": 1}}
                {"body": "Msg 2", "headers": {"count": 2}}
                {"body": "Msg 3", "headers": {"count": 3}}
                """;

            // Act
            var messages = JsonMessageParser.ParseNdjson(ndjson);

            // Assert
            messages.Should().HaveCount(3);
            // System.Text.Json deserializes all numbers as double
            messages[0].Headers!["count"].Should().Be(1.0);
            messages[1].Headers!["count"].Should().Be(2.0);
            messages[2].Headers!["count"].Should().Be(3.0);
        }
    }
}
