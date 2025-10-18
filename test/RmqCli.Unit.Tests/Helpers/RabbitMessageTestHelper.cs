using RabbitMQ.Client;

namespace RmqCli.Unit.Tests.Helpers;

/// <summary>
/// Shared test helper for creating RabbitMQ test objects.
/// </summary>
public static class RabbitMessageTestHelper
{
    /// <summary>
    /// Creates a mock IReadOnlyBasicProperties with ALL 13 properties populated.
    /// This matches all properties that the formatters check for.
    /// </summary>
    public static IReadOnlyBasicProperties CreateFullyPopulatedProperties()
    {
        var props = Substitute.For<IReadOnlyBasicProperties>();

        // 1. Type
        props.IsTypePresent().Returns(true);
        props.Type.Returns("test.type");

        // 2. MessageId
        props.IsMessageIdPresent().Returns(true);
        props.MessageId.Returns("msg-001");

        // 3. AppId
        props.IsAppIdPresent().Returns(true);
        props.AppId.Returns("test-app");

        // 4. ClusterId
        props.IsClusterIdPresent().Returns(true);
        props.ClusterId.Returns("cluster-1");

        // 5. ContentType
        props.IsContentTypePresent().Returns(true);
        props.ContentType.Returns("application/json");

        // 6. ContentEncoding
        props.IsContentEncodingPresent().Returns(true);
        props.ContentEncoding.Returns("utf-8");

        // 7. CorrelationId
        props.IsCorrelationIdPresent().Returns(true);
        props.CorrelationId.Returns("corr-123");

        // 8. DeliveryMode
        props.IsDeliveryModePresent().Returns(true);
        props.DeliveryMode.Returns(DeliveryModes.Persistent);

        // 9. Expiration
        props.IsExpirationPresent().Returns(true);
        props.Expiration.Returns("60000");

        // 10. Priority
        props.IsPriorityPresent().Returns(true);
        props.Priority.Returns((byte)5);

        // 11. ReplyTo
        props.IsReplyToPresent().Returns(true);
        props.ReplyTo.Returns("reply-queue");

        // 12. Timestamp
        props.IsTimestampPresent().Returns(true);
        props.Timestamp.Returns(new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

        // 13. Headers
        props.IsHeadersPresent().Returns(true);
        props.Headers.Returns(new Dictionary<string, object?>
        {
            ["x-custom"] = "custom-value"
        });

        return props;
    }
}
