using RabbitMQ.Client;
using RmqCli.Commands.Publish;
using RmqCli.Core.Models;

namespace RmqCli.Unit.Tests.Commands.Publish;

public class PropertyMergerTests
{
    public class MergePropertiesOnly
    {
        [Fact]
        public void UsesJsonProperties_WhenCliOptionsAreNull()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Properties = new MessageProperties
                {
                    ContentType = "application/json",
                    CorrelationId = "json-123",
                    DeliveryMode = DeliveryModes.Persistent
                }
            };
            var cliOptions = new PublishOptions();

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Properties.Should().NotBeNull();
            merged.Properties!.ContentType.Should().Be("application/json");
            merged.Properties!.CorrelationId.Should().Be("json-123");
            merged.Properties!.DeliveryMode.Should().Be(DeliveryModes.Persistent);
        }

        [Fact]
        public void UsesCliProperties_WhenJsonPropertiesAreNull()
        {
            // Arrange
            var jsonMessage = new Message { Body = "test" };
            var cliOptions = new PublishOptions
            {
                ContentType = "text/plain",
                CorrelationId = "cli-456",
                DeliveryMode = DeliveryModes.Transient
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Properties.Should().NotBeNull();
            merged.Properties!.ContentType.Should().Be("text/plain");
            merged.Properties!.CorrelationId.Should().Be("cli-456");
            merged.Properties!.DeliveryMode.Should().Be(DeliveryModes.Transient);
        }

        [Fact]
        public void CliPropertiesOverrideJsonProperties()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Properties = new MessageProperties
                {
                    ContentType = "application/json",
                    CorrelationId = "json-123",
                    DeliveryMode = DeliveryModes.Persistent
                }
            };
            var cliOptions = new PublishOptions
            {
                ContentType = "text/plain",
                CorrelationId = "cli-456"
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Properties!.ContentType.Should().Be("text/plain", "CLI should override JSON");
            merged.Properties!.CorrelationId.Should().Be("cli-456", "CLI should override JSON");
            merged.Properties!.DeliveryMode.Should().Be(DeliveryModes.Persistent, "JSON value should remain when CLI is null");
        }

        [Fact]
        public void MergesAllPropertyTypes_Correctly()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Properties = new MessageProperties
                {
                    AppId = "json-app",
                    ClusterId = "json-cluster",
                    ContentType = "application/json",
                    ContentEncoding = "utf-8",
                    CorrelationId = "json-corr",
                    DeliveryMode = DeliveryModes.Persistent,
                    Expiration = "60000",
                    Priority = 5,
                    ReplyTo = "json-reply",
                    Type = "json-type",
                    UserId = "json-user"
                }
            };
            var cliOptions = new PublishOptions
            {
                ContentType = "text/plain", // Override
                Priority = 9 // Override
                // Other properties from JSON should remain
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            var props = merged.Properties!;
            props.AppId.Should().Be("json-app");
            props.ClusterId.Should().Be("json-cluster");
            props.ContentType.Should().Be("text/plain"); // Overridden
            props.ContentEncoding.Should().Be("utf-8");
            props.CorrelationId.Should().Be("json-corr");
            props.DeliveryMode.Should().Be(DeliveryModes.Persistent);
            props.Expiration.Should().Be("60000");
            props.Priority.Should().Be(9); // Overridden
            props.ReplyTo.Should().Be("json-reply");
            props.Type.Should().Be("json-type");
            props.UserId.Should().Be("json-user");
        }

        [Fact]
        public void CreatesNewPropertiesObject_WhenBothAreNull()
        {
            // Arrange
            var jsonMessage = new Message { Body = "test" };
            var cliOptions = new PublishOptions();

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Properties.Should().NotBeNull();
        }
    }

    public class MergeHeadersOnly
    {
        [Fact]
        public void UsesJsonHeaders_WhenCliHeadersAreNull()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Headers = new Dictionary<string, object>
                {
                    { "x-json-header", "json-value" },
                    { "x-priority", 5 }
                }
            };
            var cliOptions = new PublishOptions();

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Headers.Should().NotBeNull();
            merged.Headers.Should().HaveCount(2);
            merged.Headers!["x-json-header"].Should().Be("json-value");
            merged.Headers!["x-priority"].Should().Be(5);
        }

        [Fact]
        public void UsesCliHeaders_WhenJsonHeadersAreNull()
        {
            // Arrange
            var jsonMessage = new Message { Body = "test" };
            var cliOptions = new PublishOptions
            {
                Headers = new Dictionary<string, object>
                {
                    { "x-cli-header", "cli-value" },
                    { "x-count", 10 }
                }
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Headers.Should().NotBeNull();
            merged.Headers.Should().HaveCount(2);
            merged.Headers!["x-cli-header"].Should().Be("cli-value");
            merged.Headers!["x-count"].Should().Be(10);
        }

        [Fact]
        public void CliHeadersOverrideJsonHeaders_ForSameKey()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Headers = new Dictionary<string, object>
                {
                    { "x-header", "json-value" },
                    { "x-json-only", "json" }
                }
            };
            var cliOptions = new PublishOptions
            {
                Headers = new Dictionary<string, object>
                {
                    { "x-header", "cli-value" },
                    { "x-cli-only", "cli" }
                }
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Headers.Should().HaveCount(3);
            merged.Headers!["x-header"].Should().Be("cli-value", "CLI should override JSON for same key");
            merged.Headers!["x-json-only"].Should().Be("json", "JSON-only header should remain");
            merged.Headers!["x-cli-only"].Should().Be("cli", "CLI-only header should be added");
        }

        [Fact]
        public void MergesHeaders_PreservingDifferentKeys()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Headers = new Dictionary<string, object>
                {
                    { "x-json-1", "value1" },
                    { "x-json-2", "value2" }
                }
            };
            var cliOptions = new PublishOptions
            {
                Headers = new Dictionary<string, object>
                {
                    { "x-cli-1", "value3" },
                    { "x-cli-2", "value4" }
                }
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Headers.Should().HaveCount(4);
            merged.Headers!["x-json-1"].Should().Be("value1");
            merged.Headers!["x-json-2"].Should().Be("value2");
            merged.Headers!["x-cli-1"].Should().Be("value3");
            merged.Headers!["x-cli-2"].Should().Be("value4");
        }

        [Fact]
        public void ReturnsNull_WhenBothHeadersAreNull()
        {
            // Arrange
            var jsonMessage = new Message { Body = "test" };
            var cliOptions = new PublishOptions();

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Headers.Should().BeNull();
        }

        [Fact]
        public void ReturnsNull_WhenBothHeadersAreEmpty()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Headers = new Dictionary<string, object>()
            };
            var cliOptions = new PublishOptions
            {
                Headers = new Dictionary<string, object>()
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Headers.Should().NotBeNull();
            merged.Headers.Should().BeEmpty();
        }
    }

    public class MergeCombined
    {
        [Fact]
        public void MergesBothPropertiesAndHeaders()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Properties = new MessageProperties
                {
                    ContentType = "application/json",
                    CorrelationId = "json-123"
                },
                Headers = new Dictionary<string, object>
                {
                    { "x-json-header", "json" }
                }
            };
            var cliOptions = new PublishOptions
            {
                Priority = 5,
                Headers = new Dictionary<string, object>
                {
                    { "x-cli-header", "cli" }
                }
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Properties!.ContentType.Should().Be("application/json");
            merged.Properties!.CorrelationId.Should().Be("json-123");
            merged.Properties!.Priority.Should().Be(5);
            merged.Headers.Should().HaveCount(2);
            merged.Headers!["x-json-header"].Should().Be("json");
            merged.Headers!["x-cli-header"].Should().Be("cli");
        }

        [Fact]
        public void PreservesBodyFromJsonMessage()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "Original body",
                Properties = new MessageProperties { ContentType = "text/plain" }
            };
            var cliOptions = new PublishOptions { Priority = 1 };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Body.Should().Be("Original body");
        }

        [Fact]
        public void HandlesCompleteOverride_WithAllCliOptions()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Properties = new MessageProperties
                {
                    ContentType = "application/json",
                    CorrelationId = "json-corr",
                    DeliveryMode = DeliveryModes.Transient,
                    Priority = 3
                },
                Headers = new Dictionary<string, object>
                {
                    { "x-shared", "json-value" }
                }
            };
            var cliOptions = new PublishOptions
            {
                ContentType = "text/plain",
                CorrelationId = "cli-corr",
                DeliveryMode = DeliveryModes.Persistent,
                Priority = 9,
                Headers = new Dictionary<string, object>
                {
                    { "x-shared", "cli-value" },
                    { "x-new", "new-value" }
                }
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Properties!.ContentType.Should().Be("text/plain");
            merged.Properties!.CorrelationId.Should().Be("cli-corr");
            merged.Properties!.DeliveryMode.Should().Be(DeliveryModes.Persistent);
            merged.Properties!.Priority.Should().Be(9);
            merged.Headers.Should().HaveCount(2);
            merged.Headers!["x-shared"].Should().Be("cli-value");
            merged.Headers!["x-new"].Should().Be("new-value");
        }

        [Fact]
        public void HandlesNoOverride_WhenAllCliOptionsAreNull()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Properties = new MessageProperties
                {
                    ContentType = "application/json",
                    CorrelationId = "json-123",
                    DeliveryMode = DeliveryModes.Persistent
                },
                Headers = new Dictionary<string, object>
                {
                    { "x-header", "value" }
                }
            };
            var cliOptions = new PublishOptions(); // All null

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Properties!.ContentType.Should().Be("application/json");
            merged.Properties!.CorrelationId.Should().Be("json-123");
            merged.Properties!.DeliveryMode.Should().Be(DeliveryModes.Persistent);
            merged.Headers!["x-header"].Should().Be("value");
        }
    }

    public class EdgeCases
    {
        [Fact]
        public void HandlesEmptyMessage()
        {
            // Arrange
            var jsonMessage = new Message { Body = "" };
            var cliOptions = new PublishOptions();

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Body.Should().Be("");
            merged.Properties.Should().NotBeNull();
            merged.Headers.Should().BeNull();
        }

        [Fact]
        public void DoesNotModifyOriginalJsonMessage()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Properties = new MessageProperties { ContentType = "application/json" },
                Headers = new Dictionary<string, object> { { "x-header", "value" } }
            };
            var cliOptions = new PublishOptions
            {
                ContentType = "text/plain",
                Headers = new Dictionary<string, object> { { "x-header", "new-value" } }
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert - original should be unchanged
            jsonMessage.Properties!.ContentType.Should().Be("application/json");
            jsonMessage.Headers!["x-header"].Should().Be("value");

            // Merged should have CLI values
            merged.Properties!.ContentType.Should().Be("text/plain");
            merged.Headers!["x-header"].Should().Be("new-value");
        }

        [Fact]
        public void HandlesNullableIntegerProperties_Correctly()
        {
            // Arrange
            var jsonMessage = new Message
            {
                Body = "test",
                Properties = new MessageProperties
                {
                    DeliveryMode = DeliveryModes.Transient,
                    Priority = 3
                }
            };
            var cliOptions = new PublishOptions
            {
                DeliveryMode = DeliveryModes.Persistent
                // Priority not set (null)
            };

            // Act
            var merged = PropertyMerger.Merge(jsonMessage, cliOptions);

            // Assert
            merged.Properties!.DeliveryMode.Should().Be(DeliveryModes.Persistent, "CLI should override");
            merged.Properties!.Priority.Should().Be(3, "JSON value should remain when CLI is null");
        }
    }
}
