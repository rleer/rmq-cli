using Xunit.Abstractions;

namespace Rmq.E2E.Tests;

/// <summary>
/// The tests confidence actually comes from: a real broker, the published binary, and
/// assertions made over AMQP rather than through the tool being tested.
/// </summary>
[Collection(RabbitMqCollection.Name)]
public class PublishConsumeTests(RabbitMqFixture fixture, ITestOutputHelper output)
{
    private readonly Cli _rmq = new(output);
    private readonly Broker _broker = new(fixture);

    private string[] Url => ["--url", fixture.AmqpUrl];

    private static string QueueName([System.Runtime.CompilerServices.CallerMemberName] string caller = "") =>
        $"rmq-e2e-{caller}-{Guid.NewGuid():N}";

    [Fact]
    public async Task Publish_then_consume_round_trips_a_message()
    {
        var queue = await _broker.DeclareQueue(QueueName());

        var published = await _rmq.Run([.. Url, "publish", "-q", queue, "--body", "hello world"]);
        published.ExitCode.Should().Be(0);

        var consumed = await _rmq.Run([.. Url, "consume", "-q", queue, "--json"]);

        consumed.ExitCode.Should().Be(0);
        consumed.StdoutLines.Should().ContainSingle()
            .Which.Should().Be($$"""{"body":"hello world","routingKey":"{{queue}}","exchange":""}""");

        (await _broker.Depth(queue)).Should().Be(0, "consume acks what it writes");

        await _broker.DeleteQueue(queue);
    }

    [Fact]
    public async Task Pull_round_trips_a_message_without_registering_a_consumer()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        await _rmq.Run([.. Url, "publish", "-q", queue, "--body", "pulled"]);

        var consumed = await _rmq.Run([.. Url, "consume", "-q", queue, "--pull", "--json"]);

        consumed.ExitCode.Should().Be(0);
        consumed.StdoutLines.Should().ContainSingle().Which.Should().Contain("\"body\":\"pulled\"");
        (await _broker.Depth(queue)).Should().Be(0);

        await _broker.DeleteQueue(queue);
    }

    /// <summary>
    /// The composition CLAUDE.md names as supported and tested: consume writes exactly what
    /// publish reads. Source and destination differ on purpose — with one queue the test
    /// would pass without proving the routing target comes from -q rather than the message.
    /// </summary>
    [Fact]
    public async Task Every_property_survives_the_consume_publish_pipe()
    {
        var source = await _broker.DeclareQueue(QueueName() + "-src");
        var destination = await _broker.DeclareQueue(QueueName() + "-dst");

        const string Message = """
            {"body":{"orderId":42},"properties":{"contentType":"application/json","contentEncoding":"identity","deliveryMode":2,"priority":7,"correlationId":"c-1","replyTo":"replies","expiration":"600000","messageId":"m-1","timestamp":1700000000,"type":"order.created","appId":"shop","headers":{"x-source":"web","x-attempt":3,"x-ratio":1.5,"x-flag":true}}}
            """;

        (await _rmq.Run([.. Url, "publish", "-q", source, "--message", Message])).ExitCode.Should().Be(0);

        var drained = await _rmq.Run([.. Url, "consume", "-q", source, "--json"]);
        drained.ExitCode.Should().Be(0);

        var republished = await _rmq.Run([.. Url, "publish", "-q", destination], stdin: drained.Stdout);
        republished.ExitCode.Should().Be(0);

        var final = await _rmq.Run([.. Url, "consume", "-q", destination, "--json"]);

        // Only the routing key differs, because the destination is the command line's.
        final.StdoutLines.Should().ContainSingle()
            .Which.Should().Be(drained.StdoutLines.Single().Replace($"\"{source}\"", $"\"{destination}\""));

        await _broker.DeleteQueue(source);
        await _broker.DeleteQueue(destination);
    }

    [Fact]
    public async Task Binary_bodies_round_trip_byte_identically()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        byte[] body = [0xFF, 0xFE, 0x00, 0x01, 0x80];
        await _broker.PublishBytes(queue, body);

        var consumed = await _rmq.Run([.. Url, "consume", "-q", queue, "--json"]);
        consumed.StdoutLines.Should().ContainSingle()
            .Which.Should().Contain($"\"body\":\"{Convert.ToBase64String(body)}\"")
            .And.Contain("\"bodyEncoding\":\"base64\"");

        var republished = await _rmq.Run([.. Url, "publish", "-q", queue], stdin: consumed.Stdout);
        republished.ExitCode.Should().Be(0);

        // Same base64 out as went in: the bytes never became replacement characters.
        var again = await _rmq.Run([.. Url, "consume", "-q", queue, "--json"]);
        again.StdoutLines.Should().ContainSingle().Which.Should().Be(consumed.StdoutLines.Single());

        await _broker.DeleteQueue(queue);
    }

    /// <summary>
    /// The regression guard for the nack-loop trap: --requeue must terminate *and* leave the
    /// queue exactly as it found it.
    /// </summary>
    [Fact]
    public async Task Requeue_drains_to_stdout_but_leaves_the_queue_intact()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        await _broker.Publish(queue, "one", "two", "three");

        var consumed = await _rmq.Run([.. Url, "consume", "-q", queue, "--requeue", "--json"], timeout: TimeSpan.FromSeconds(30));

        consumed.ExitCode.Should().Be(0);
        consumed.StdoutLines.Should().HaveCount(3, "--requeue drains by default, exactly like a normal consume");
        consumed.Stderr.Should().Contain("unacked", "an unbounded --requeue warns about memory growth");

        (await _broker.Depth(queue)).Should().Be(3, "nothing was acked, so the broker requeued everything");

        await _broker.DeleteQueue(queue);
    }

    [Fact]
    public async Task Count_larger_than_the_queue_exits_three()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        await _broker.Publish(queue, "one", "two");

        var consumed = await _rmq.Run([.. Url, "consume", "-q", queue, "--count", "10", "--json"]);

        consumed.ExitCode.Should().Be(3, "code 3 distinguishes a short read from a failure");
        consumed.StdoutLines.Should().HaveCount(2);

        await _broker.DeleteQueue(queue);
    }

    [Fact]
    public async Task Count_satisfied_exits_zero()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        await _broker.Publish(queue, "one", "two", "three");

        var consumed = await _rmq.Run([.. Url, "consume", "-q", queue, "--count", "2", "--json"]);

        consumed.ExitCode.Should().Be(0);
        consumed.StdoutLines.Should().HaveCount(2);

        await _broker.DeleteQueue(queue);
    }

    [Fact]
    public async Task Publishing_to_a_queue_that_does_not_exist_fails()
    {
        var result = await _rmq.Run([.. Url, "publish", "-q", QueueName(), "--body", "nowhere"]);

        result.ExitCode.Should().Be(1);
        result.Stderr.Should().Contain("NOT_FOUND");
    }

    [Fact]
    public async Task To_file_writes_ndjson_and_leaves_stdout_empty()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        await _broker.Publish(queue, "one", "two");
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ndjson");

        var consumed = await _rmq.Run([.. Url, "consume", "-q", queue, "--to-file", path]);

        consumed.ExitCode.Should().Be(0);
        consumed.Stdout.Should().BeEmpty();
        File.ReadAllLines(path).Should().HaveCount(2);

        File.Delete(path);
        await _broker.DeleteQueue(queue);
    }

    [Fact]
    public async Task Follow_keeps_waiting_and_exits_130_on_ctrl_c()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        await _broker.Publish(queue, "first");

        using var interrupt = new CancellationTokenSource();
        var running = _rmq.Run([.. Url, "consume", "-q", queue, "--follow", "--json"], cancel: interrupt.Token);

        // Well past the idle window a bare consume would have exited on.
        await Task.Delay(TimeSpan.FromSeconds(4));
        await interrupt.CancelAsync();

        var consumed = await running;

        consumed.ExitCode.Should().Be(130);
        consumed.StdoutLines.Should().ContainSingle().Which.Should().Contain("\"body\":\"first\"");
        (await _broker.Depth(queue)).Should().Be(0, "Ctrl-C acks everything already written");

        await _broker.DeleteQueue(queue);
    }

    [Fact]
    public async Task Purge_empties_the_queue()
    {
        var queue = await _broker.DeclareQueue(QueueName());
        await _broker.Publish(queue, "one", "two", "three");

        var purged = await _rmq.Run([.. Url, "purge", queue]);

        purged.ExitCode.Should().Be(0);
        purged.Stderr.Should().Contain("3");
        (await _broker.Depth(queue)).Should().Be(0);

        await _broker.DeleteQueue(queue);
    }
}
