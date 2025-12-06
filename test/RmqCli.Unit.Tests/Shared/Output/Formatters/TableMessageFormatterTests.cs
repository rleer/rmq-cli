using System.Text;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Shared.Output;
using RmqCli.Shared.Output.Formatters;
using RmqCli.Unit.Tests.Helpers;
using Spectre.Console;
using Xunit.Abstractions;

namespace RmqCli.Unit.Tests.Shared.Output.Formatters;

public class TableMessageFormatterTests
{
    public class FormatMessage
    {
        [Fact]
        public void IncludesMessageDeliveryTagAsHeader()
        {
            // Arrange
            var message = CreateRetrievedMessage("test", deliveryTag: 42);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Message #42");
        }

        [Fact]
        public void IncludesQueue()
        {
            // Arrange
            var message = CreateRetrievedMessage("Test message body", queue: "my-queue");

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("my-queue");
        }

        [Fact]
        public void IncludesRoutingKey()
        {
            // Arrange
            var message = CreateRetrievedMessage("Test message body", routingKey: "test.routing.key");

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("test.routing.key");
        }

        [Fact]
        public void IncludesExchange()
        {
            // Arrange
            var message = CreateRetrievedMessage("Test message body", exchange: "test.exchange");

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("test.exchange");
        }

        [Fact]
        public void ShowsDashForEmptyExchange()
        {
            // Arrange
            var message = CreateRetrievedMessage("Test message body", exchange: "");

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);

            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          -                                                          │
                               │ Redelivered       No                                                         │
                               │ ── Body (17 bytes) ───────────────────────────────────────────────────────── │
                               │ Test message body                                                            │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void IncludesRedeliveredStatus_WhenTrue()
        {
            // Arrange
            var message = CreateRetrievedMessage("test", redelivered: true);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Yes");
        }

        [Fact]
        public void IncludesRedeliveredStatus_WhenFalse()
        {
            // Arrange
            var message = CreateRetrievedMessage("test", redelivered: false);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("No");
        }

        [Fact]
        public void IncludesMessageBody()
        {
            // Arrange
            var message = CreateRetrievedMessage("Test message body");

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Test message body");
        }

        [Fact]
        public void IncludesBodySize()
        {
            // Arrange
            var message = CreateRetrievedMessage("Test");

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Body (4 bytes)");
        }

        [Fact]
        public void HandlesEmptyBody()
        {
            // Arrange
            var message = CreateRetrievedMessage("");

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);

            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Body (0 bytes) ────────────────────────────────────────────────────────── │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void EscapesMarkupCharactersInBody()
        {
            // Arrange
            var bodyWithMarkup = "[bold]This should not be bold[/]";
            var message = CreateRetrievedMessage(bodyWithMarkup);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            // The markup should be escaped and visible as plain text
            result.Should().Contain("[bold]This should not be bold[/]");
        }

        [Fact]
        public void HandlesMultilineBody()
        {
            // Arrange
            var multilineBody = "Line 1\nLine 2\nLine 3";
            var message = CreateRetrievedMessage(multilineBody);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);

            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Body (20 bytes) ───────────────────────────────────────────────────────── │
                               │ Line 1                                                                       │
                               │ Line 2                                                                       │
                               │ Line 3                                                                       │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }
    }

    public class FormatMessageWithProperties
    {
        [Fact]
        public void ShowsPropertiesSection_WhenPropertiesExist()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Properties");
            result.Should().Contain("msg-123");
        }

        [Fact]
        public void ShowsMessageId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-001");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("msg-001");
        }

        [Fact]
        public void ShowsCorrelationId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsCorrelationIdPresent().Returns(true);
            props.CorrelationId.Returns("corr-123");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("corr-123");
        }

        [Fact]
        public void ShowsTimestamp_WhenPresent()
        {
            // Arrange
            var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.Zero);
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsTimestampPresent().Returns(true);
            props.Timestamp.Returns(new AmqpTimestamp(timestamp.ToUnixTimeSeconds()));

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("2024-01-15 10:30:45 UTC");
        }

        [Fact]
        public void ShowsContentType_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsContentTypePresent().Returns(true);
            props.ContentType.Returns("application/json");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("application/json");
        }

        [Fact]
        public void ShowsContentEncoding_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsContentEncodingPresent().Returns(true);
            props.ContentEncoding.Returns("utf-8");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("utf-8");
        }

        [Fact]
        public void ShowsDeliveryMode_Transient()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsDeliveryModePresent().Returns(true);
            props.DeliveryMode.Returns(DeliveryModes.Transient);

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Non-persistent (1)");
        }

        [Fact]
        public void ShowsDeliveryMode_Persistent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsDeliveryModePresent().Returns(true);
            props.DeliveryMode.Returns(DeliveryModes.Persistent);

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("Persistent (2)");
        }

        [Fact]
        public void ShowsPriority_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsPriorityPresent().Returns(true);
            props.Priority.Returns((byte)5);

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("5");
        }

        [Fact]
        public void ShowsExpiration_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsExpirationPresent().Returns(true);
            props.Expiration.Returns("60000");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("60000");
        }

        [Fact]
        public void ShowsReplyTo_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsReplyToPresent().Returns(true);
            props.ReplyTo.Returns("reply-queue");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("reply-queue");
        }

        [Fact]
        public void ShowsType_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsTypePresent().Returns(true);
            props.Type.Returns("user.created");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("user.created");
        }

        [Fact]
        public void ShowsAppId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsAppIdPresent().Returns(true);
            props.AppId.Returns("my-app");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("my-app");
        }

        [Fact]
        public void ShowsUserId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsUserIdPresent().Returns(true);
            props.UserId.Returns("guest");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("guest");
        }

        [Fact]
        public void ShowsClusterId_WhenPresent()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsClusterIdPresent().Returns(true);
            props.ClusterId.Returns("cluster-1");

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message);

            // Assert
            result.Should().Contain("cluster-1");
        }
    }

    public class FormatMessageWithHeaders
    {
        private readonly ITestOutputHelper _output;

        public FormatMessageWithHeaders(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public void ShowsCustomHeadersSection_WhenHeadersExist()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-custom"] = "custom-value"
            });

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);

            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Custom Headers ────────────────────────────────────────────────────────── │
                               │ x-custom          custom-value                                               │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void ShowsMultipleHeaders()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-header-1"] = "value1",
                ["x-header-2"] = "value2",
                ["x-header-3"] = "value3"
            });

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);

            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Custom Headers ────────────────────────────────────────────────────────── │
                               │ x-header-1        value1                                                     │
                               │ x-header-2        value2                                                     │
                               │ x-header-3        value3                                                     │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void FormatsArrayWithComplexObjectsMultiLine()
        {
            // Arrange - Arrays containing complex objects are formatted multi-line
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>
            {
                ["x-array-objects"] = new object[]
                {
                    new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 },
                    new Dictionary<string, object> { ["name"] = "Bob", ["age"] = 25 }
                }
            });

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);

            // Assert - Verify proper multi-line formatting with correct indentation (platform-independent)
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Custom Headers ────────────────────────────────────────────────────────── │
                               │ x-array-objects   [                                                          │
                               │                     {name: Alice, age: 30}                                   │
                               │                     {name: Bob, age: 25}                                     │
                               │                   ]                                                          │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯ 
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void DoesNotShowHeadersSection_WhenNoHeaders()
        {
            // Arrange
            var message = CreateRetrievedMessage("test", props: null);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);
            _output.WriteLine(result);
            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void DoesNotShowHeadersSection_WhenHeadersEmpty()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsHeadersPresent().Returns(true);
            props.Headers.Returns(new Dictionary<string, object?>());

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);
            _output.WriteLine(result);
            
            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
            result.Should().NotContain("Custom Headers");
        }
    }

    public class FormatMessageCompactMode
    {
        private readonly ITestOutputHelper _output;
        public FormatMessageCompactMode(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public void CompactMode_ShowsOnlyPropertiesWithValues()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");
            props.IsCorrelationIdPresent().Returns(false);

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);
            _output.WriteLine(result);
            
            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Properties ────────────────────────────────────────────────────────────── │
                               │ Message ID        msg-123                                                    │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void CompactMode_HidesPropertiesSection_WhenNoPropertiesSet()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            // No properties set
            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);
            _output.WriteLine(result);

            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void NonCompactMode_ShowsAllProperties_WithDashesForMissing()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");
            // Other properties not set

            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: false, AnsiSupport.No);

            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Properties ────────────────────────────────────────────────────────────── │
                               │ Message ID        msg-123                                                    │
                               │ Correlation ID    -                                                          │
                               │ Timestamp         -                                                          │
                               │ Content Type      -                                                          │
                               │ Content Encoding  -                                                          │
                               │ Delivery Mode     -                                                          │
                               │ Priority          -                                                          │
                               │ Expiration        -                                                          │
                               │ Reply To          -                                                          │
                               │ Type              -                                                          │
                               │ App ID            -                                                          │
                               │ User ID           -                                                          │
                               │ Cluster ID        -                                                          │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void NonCompactMode_ShowsPropertiesSection_EvenWhenNoPropertiesSet()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: false, AnsiSupport.No);

            // Assert
            const string msg = """
                               ╭─Message #1───────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Properties ────────────────────────────────────────────────────────────── │
                               │ Message ID        -                                                          │
                               │ Correlation ID    -                                                          │
                               │ Timestamp         -                                                          │
                               │ Content Type      -                                                          │
                               │ Content Encoding  -                                                          │
                               │ Delivery Mode     -                                                          │
                               │ Priority          -                                                          │
                               │ Expiration        -                                                          │
                               │ Reply To          -                                                          │
                               │ Type              -                                                          │
                               │ App ID            -                                                          │
                               │ User ID           -                                                          │
                               │ Cluster ID        -                                                          │
                               │ ── Body (4 bytes) ────────────────────────────────────────────────────────── │
                               │ test                                                                         │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void CompactMode_ShowsAllProperties_WhenAllPropertiesSet()
        {
            // Arrange
            var props = RabbitMessageTestHelper.CreateFullyPopulatedProperties();
            var message = CreateRetrievedMessage("test", props: props);

            // Act
            var result = TableMessageFormatter.FormatMessage(message, compact: true, AnsiSupport.No);

            // Assert
            result.Should().Contain("""
                                    │ ── Properties ────────────────────────────────────────────────────────────── │
                                    │ Message ID        msg-001                                                    │
                                    │ Correlation ID    corr-123                                                   │
                                    │ Timestamp         2025-12-06 00:00:00 UTC                                    │
                                    │ Content Type      application/json                                           │
                                    │ Content Encoding  utf-8                                                      │
                                    │ Delivery Mode     Persistent (2)                                             │
                                    │ Priority          5                                                          │
                                    │ Expiration        60000                                                      │
                                    │ Reply To          reply-queue                                                │
                                    │ Type              test.type                                                  │
                                    │ App ID            test-app                                                   │
                                    │ User ID           user-123                                                   │
                                    │ Cluster ID        cluster-1                                                  │
                                    │ ── Custom Headers ────────────────────────────────────────────────────────── │
                                    │ x-custom          custom-value                                               │
                                    """);
        }
    }

    public class FormatMessages
    {
        [Fact]
        public void ReturnsEmptyString_WhenNoMessages()
        {
            // Arrange
            var messages = Array.Empty<RetrievedMessage>();

            // Act
            var result = TableMessageFormatter.FormatMessages(messages);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void HandlesSingleMessage()
        {
            // Arrange
            var messages = new[] { CreateRetrievedMessage("Only one", deliveryTag: 99) };

            // Act
            var result = TableMessageFormatter.FormatMessages(messages, ansiSupport: AnsiSupport.No);

            // Assert
            const string msg = """
                               ╭─Message #99──────────────────────────────────────────────────────────────────╮
                               │ Queue             test-queue                                                 │
                               │ Routing Key       routing.key                                                │
                               │ Exchange          exchange                                                   │
                               │ Redelivered       No                                                         │
                               │ ── Properties ────────────────────────────────────────────────────────────── │
                               │ Message ID        -                                                          │
                               │ Correlation ID    -                                                          │
                               │ Timestamp         -                                                          │
                               │ Content Type      -                                                          │
                               │ Content Encoding  -                                                          │
                               │ Delivery Mode     -                                                          │
                               │ Priority          -                                                          │
                               │ Expiration        -                                                          │
                               │ Reply To          -                                                          │
                               │ Type              -                                                          │
                               │ App ID            -                                                          │
                               │ User ID           -                                                          │
                               │ Cluster ID        -                                                          │
                               │ ── Body (8 bytes) ────────────────────────────────────────────────────────── │
                               │ Only one                                                                     │
                               ╰──────────────────────────────────────────────────────────────────────────────╯
                               """;
            result.Should().Be(msg.TrimEnd());
        }

        [Fact]
        public void AppliesCompactMode_ToAllMessages()
        {
            // Arrange
            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-1");

            var messages = new[]
            {
                CreateRetrievedMessage("First", deliveryTag: 1, props: props),
                CreateRetrievedMessage("Second", deliveryTag: 2, props: props)
            };

            // Act
            var result = TableMessageFormatter.FormatMessages(messages, compact: true, ansiSupport: AnsiSupport.No);

            // Assert
            const string msg1 = """
                                ╭─Message #1───────────────────────────────────────────────────────────────────╮
                                │ Queue             test-queue                                                 │
                                │ Routing Key       routing.key                                                │
                                │ Exchange          exchange                                                   │
                                │ Redelivered       No                                                         │
                                │ ── Properties ────────────────────────────────────────────────────────────── │
                                │ Message ID        msg-1                                                      │
                                │ ── Body (5 bytes) ────────────────────────────────────────────────────────── │
                                │ First                                                                        │
                                ╰──────────────────────────────────────────────────────────────────────────────╯
                                """;
            const string msg2 = """
                                ╭─Message #2───────────────────────────────────────────────────────────────────╮
                                │ Queue             test-queue                                                 │
                                │ Routing Key       routing.key                                                │
                                │ Exchange          exchange                                                   │
                                │ Redelivered       No                                                         │
                                │ ── Properties ────────────────────────────────────────────────────────────── │
                                │ Message ID        msg-1                                                      │
                                │ ── Body (6 bytes) ────────────────────────────────────────────────────────── │
                                │ Second                                                                       │
                                ╰──────────────────────────────────────────────────────────────────────────────╯
                                """;
            result.Should().Contain(msg1.TrimEnd());
            result.Should().Contain(msg2.TrimEnd());
        }
    }

    #region Test Helpers

    private static RetrievedMessage CreateRetrievedMessage(
        string body,
        string exchange = "exchange",
        string routingKey = "routing.key",
        string queue = "test-queue",
        ulong deliveryTag = 1,
        IReadOnlyBasicProperties? props = null,
        bool redelivered = false)
    {
        var (properties, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);
        var bodySizeBytes = Encoding.UTF8.GetByteCount(body);

        return new RetrievedMessage
        {
            Body = body,
            Exchange = exchange,
            RoutingKey = routingKey,
            Queue = queue,
            DeliveryTag = deliveryTag,
            Properties = properties,
            Headers = headers,
            Redelivered = redelivered,
            BodySizeBytes = bodySizeBytes,
            BodySize = OutputUtilities.ToSizeString(bodySizeBytes)
        };
    }

    #endregion
}