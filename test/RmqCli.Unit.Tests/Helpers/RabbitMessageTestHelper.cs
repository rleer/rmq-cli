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

        var date = new DateTimeOffset(2025, 12, 6, 0, 0, 0, TimeSpan.Zero);
        props.IsTimestampPresent().Returns(true);
        props.Timestamp.Returns(new AmqpTimestamp(date.ToUnixTimeSeconds()));

        props.IsTypePresent().Returns(true);
        props.Type.Returns("test.type");
        
        props.IsUserIdPresent().Returns(true);
        props.UserId.Returns("user-123");
        
        props.IsHeadersPresent().Returns(true);
        props.Headers.Returns(new Dictionary<string, object?>
        {
            ["x-custom"] = "custom-value"
        });

        return props;
    }
}
