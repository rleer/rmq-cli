using Xunit.Abstractions;

namespace Rmq.E2E.Tests;

/// <summary>
/// The HTTP transport is a degraded fallback, not a co-equal path, so this is deliberately
/// two tests rather than a parallel suite: one round trip proving the schema is genuinely
/// the same schema, and one proving --requeue stays inside the bound it cannot escape.
/// </summary>
[Collection(RabbitMqCollection.Name)]
public class HttpTransportTests(RabbitMqFixture fixture, ITestOutputHelper output)
{
    private readonly Cli _rmq = new(output);
    private readonly Broker _broker = new(fixture);

    private string[] Http => ["--url", fixture.ManagementUrl];

    private static string QueueName([System.Runtime.CompilerServices.CallerMemberName] string caller = "") =>
        $"rmq-e2e-{caller}-{Guid.NewGuid():N}";

    /// <summary>
    /// Two messages on purpose. The first carries the full property set including a nested
    /// header table and a list — the shapes whose source-generation metadata is registered
    /// per-context, so a missing registration fails on this path alone. The second carries
    /// a binary body and no properties at all, which is how the management API's two
    /// encoding quirks surface: payload_encoding base64, and an empty property set arriving
    /// as [] rather than {}.
    /// </summary>
    [Fact]
    public async Task Http_transport_round_trips_every_property_and_a_binary_body()
    {
        var queue = await _broker.DeclareQueue(QueueName());

        const string Rich = """
            {"body":{"orderId":42},"properties":{"contentType":"application/json","contentEncoding":"identity","deliveryMode":2,"priority":7,"correlationId":"c-1","replyTo":"replies","expiration":"600000","messageId":"m-1","timestamp":1700000000,"type":"order.created","appId":"shop","headers":{"x-source":"web","x-attempt":3,"x-ratio":1.5,"x-flag":true,"x-nested":{"inner":"v"},"x-list":["a",1]}}}
            """;
        const string Binary = """{"body":"//4AAYA=","bodyEncoding":"base64"}""";

        (await _rmq.Run([.. Http, "publish", "-q", queue, "--message", Rich])).ExitCode.Should().Be(0);
        (await _rmq.Run([.. Http, "publish", "-q", queue, "--message", Binary])).ExitCode.Should().Be(0);

        var consumed = await _rmq.Run([.. Http, "consume", "-q", queue, "--json"]);

        consumed.ExitCode.Should().Be(0);
        consumed.StdoutLines.Should().HaveCount(2);

        // Three '$' because the expected line itself contains "}}".
        consumed.StdoutLines[0].Should().Be(
            $$$"""{"body":{"orderId":42},"properties":{"contentType":"application/json","contentEncoding":"identity","deliveryMode":2,"priority":7,"correlationId":"c-1","replyTo":"replies","expiration":"600000","messageId":"m-1","timestamp":1700000000,"type":"order.created","appId":"shop","headers":{"x-attempt":3,"x-flag":true,"x-list":["a",1],"x-nested":{"inner":"v"},"x-ratio":1.5,"x-source":"web"}},"routingKey":"{{{queue}}}","exchange":""}""");

        // No "properties":{} — the [] an empty proplist encodes to has to read as "none".
        consumed.StdoutLines[1].Should().Be(
            $$"""{"body":"//4AAYA=","bodyEncoding":"base64","routingKey":"{{queue}}","exchange":""}""");

        (await _broker.Depth(queue)).Should().Be(0, "ack_requeue_false deletes what it returns");

        await _broker.DeleteQueue(queue);
    }

    /// <summary>
    /// The regression guard for the trap that mirrors AMQP's nack loop: ackmode
    /// ack_requeue_true hands messages straight back, so looping would re-read the same
    /// ones forever. One batch, a warning, and the queue exactly as it was found.
    /// </summary>
    [Fact]
    public async Task Requeue_over_http_reads_one_batch_and_says_it_did_not_drain()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        await _broker.Publish(queue, "one", "two", "three");

        var consumed = await _rmq.Run([.. Http, "consume", "-q", queue, "--requeue", "--json"]);

        consumed.ExitCode.Should().Be(0, "no --count was given, so nothing went unsatisfied");
        consumed.StdoutLines.Should().HaveCount(3);
        consumed.Stderr.Should().Contain("cannot drain", "the user has to be told the queue is untouched");

        // Asserted over AMQP: the management API's own message counts are sampled, and were
        // observed reporting 0 for a queue holding three requeued messages.
        (await _broker.Depth(queue)).Should().Be(3);

        await _broker.DeleteQueue(queue);
    }
}
