using System.CommandLine;
using System.Threading.Channels;
using RabbitMQ.Client;

namespace Rmq;

public static class ConsumeCommand
{
    /// <summary>
    /// How long the push path waits with no delivery before calling the queue empty. It is
    /// also the only thing that can signal exit code 3 there, since nothing else
    /// distinguishes "drained early" from "still waiting".
    /// </summary>
    private static readonly TimeSpan IdleWindow = TimeSpan.FromSeconds(1);

    /// <summary>Gap between empty gets on the pull path under --follow.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>Above this, --count is large enough that holding it all unacked is worth a warning.</summary>
    private const int LargeRequeueCount = 1000;

    public static Command Create()
    {
        var queue = new Option<string>("--queue", "-q")
        {
            Description = "Queue to consume from",
            Required = true
        };

        var count = new Option<int?>("--count", "-n")
        {
            Description = "Stop after N messages. Without it, consume drains the queue and exits."
        };

        var requeue = new Option<bool>("--requeue")
        {
            Description = "Read without consuming: nothing is acked, and everything returns to the queue on exit, flagged redelivered."
        };

        var follow = new Option<bool>("--follow", "-f")
        {
            Description = "Keep waiting for new messages. Exits on Ctrl-C only."
        };

        var toFile = new Option<string?>("--to-file")
        {
            Description = "Write NDJSON to this file instead of stdout. One file, no rotation."
        };

        var pull = new Option<bool>("--pull")
        {
            Description = "Use the polling basic.get API instead of registering as a consumer. The right choice for inspecting a queue that has real consumers on it."
        };

        var consumerPriority = new Option<int>("--consumer-priority")
        {
            Description = "x-priority for the push path (default: 0, the broker's own). Negative values yield to existing consumers. Ignored with --pull and --transport http."
        };

        var command = new Command("consume", """
            Consume messages from a queue.

            Exits when the queue is empty, so `rmq consume -q orders | jq` terminates on its
            own. With --count N it exits at N, or with code 3 if the queue empties first.
            Ctrl-C always exits cleanly, having acked everything already written.

            By default rmq registers as a consumer (basic.consume): it joins round-robin
            distribution alongside any existing consumers and the broker pushes messages at
            it continuously. Use --pull to inspect a queue that production depends on — it
            registers nothing and takes only what is asked for. Use --requeue to give
            everything back.
            """);

        command.Add(queue);
        command.Add(count);
        command.Add(requeue);
        command.Add(follow);
        command.Add(toFile);
        command.Add(pull);
        command.Add(consumerPriority);

        // The only flag-combination rule consume has. Holding an entire queue unacked
        // indefinitely is worse than holding prefetch-many.
        command.Validators.Add(result =>
        {
            if (result.GetValue(requeue) && result.GetValue(follow))
            {
                result.AddError("--requeue and --follow are mutually exclusive");
            }

            if (result.GetValue(count) is { } limit && limit <= 0)
            {
                result.AddError("--count must be greater than zero");
            }
        });

        command.SetAction((parse, ct) => Run(
            GlobalOptions.Settings(parse),
            parse.GetRequiredValue(queue),
            parse.GetValue(count),
            parse.GetValue(requeue),
            parse.GetValue(follow),
            parse.GetValue(toFile),
            parse.GetValue(pull),
            parse.GetValue(consumerPriority),
            MessageWriter.Create(parse.GetValue(GlobalOptions.Json), parse.GetValue(GlobalOptions.Raw), parse.GetValue(toFile)),
            ct));

        return command;
    }

    private static async Task<int> Run(
        ConnectionSettings settings,
        string queue,
        int? limit,
        bool requeue,
        bool follow,
        string? toFile,
        bool pull,
        int consumerPriority,
        MessageWriter writer,
        CancellationToken ct)
    {
        if (requeue)
        {
            // The broker holds everything unacked until the channel closes, and AMQP has no
            // cursor that would avoid it.
            if (limit == null)
            {
                Log.Warn("--requeue holds every message in the queue unacked until exit; memory grows with queue depth");
            }
            else if (limit >= LargeRequeueCount)
            {
                Log.Warn($"--requeue holds all {limit} messages unacked until exit; memory grows with queue depth");
            }
        }

        await using var _ = writer;
        await using var connection = await Amqp.ConnectAsync(settings, ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var consumed = pull
            ? await Pull(channel, queue, limit, requeue, follow, writer, ct)
            : await Push(channel, queue, limit, requeue, follow, consumerPriority, writer, ct);

        if (toFile != null)
        {
            Log.Debug($"wrote {consumed} messages to {toFile}");
        }

        // Code 3 is the one that matters for pipelines: fewer messages than asked for is
        // not the same as a failure.
        return limit != null && consumed < limit ? ExitCode.Incomplete : ExitCode.Success;
    }

    /// <summary>
    /// The pull path. The receive call is literal: one basic.get, one message, nothing
    /// registered on the queue.
    /// </summary>
    private static async Task<int> Pull(
        IChannel channel,
        string queue,
        int? limit,
        bool requeue,
        bool follow,
        MessageWriter writer,
        CancellationToken ct)
    {
        var consumed = 0;

        try
        {
            while (limit == null || consumed < limit)
            {
                var result = await channel.BasicGetAsync(queue, autoAck: false, ct);
                if (result == null)
                {
                    if (!follow)
                    {
                        break;
                    }

                    await Task.Delay(PollInterval, ct);
                    continue;
                }

                var message = Message.FromBytes(
                    result.Body.Span,
                    Amqp.ToProperties(result.BasicProperties),
                    result.Exchange,
                    result.RoutingKey,
                    result.Redelivered);

                await Emit(writer, channel, message, result.DeliveryTag, requeue);
                consumed++;
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl-C. Everything written is already acked; the rest was never taken.
            Log.Debug($"interrupted after {consumed} messages");
        }

        return consumed;
    }

    /// <summary>
    /// The push path. Deliveries arrive on a callback that must not block, so a single
    /// bounded channel adapts them to the same sequential loop. That one channel is the
    /// whole adapter — no writer task, no ack dispatcher.
    /// </summary>
    private static async Task<int> Push(
        IChannel channel,
        string queue,
        int? limit,
        bool requeue,
        bool follow,
        int consumerPriority,
        MessageWriter writer,
        CancellationToken ct)
    {
        // Under --requeue prefetch must be unlimited, or the broker sends prefetch-many,
        // receives no acks, and delivery stalls mid-queue. The buffer below must not follow
        // it to zero, or it becomes unbounded and client memory grows with queue depth.
        await channel.BasicQosAsync(0, requeue ? (ushort)0 : Amqp.PrefetchCount, global: false, ct);

        var buffer = Channel.CreateBounded<Delivery>(new BoundedChannelOptions(Amqp.BufferSize)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var consumer = new BufferingConsumer(channel, buffer.Writer);

        var arguments = consumerPriority != 0
            ? new Dictionary<string, object?> { ["x-priority"] = consumerPriority }
            : null;

        var consumerTag = await channel.BasicConsumeAsync(
            queue,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: arguments,
            consumer: consumer,
            cancellationToken: ct);

        Log.Debug($"consuming as '{consumerTag}' with prefetch {(requeue ? 0 : Amqp.PrefetchCount)}");

        var consumed = 0;

        try
        {
            while (limit == null || consumed < limit)
            {
                var delivery = await Receive(buffer.Reader, follow, ct);
                if (delivery == null)
                {
                    break;
                }

                await Emit(writer, channel, delivery.Message, delivery.DeliveryTag, requeue);
                consumed++;
            }
        }
        catch (OperationCanceledException)
        {
            Log.Debug($"interrupted after {consumed} messages");
        }

        return consumed;
    }

    /// <summary>
    /// Null means stop: the queue went idle, or the channel closed under us. Under --follow
    /// only the channel closing ends the wait.
    /// </summary>
    private static async Task<Delivery?> Receive(ChannelReader<Delivery> reader, bool follow, CancellationToken ct)
    {
        try
        {
            if (follow)
            {
                return await reader.ReadAsync(ct);
            }

            using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
            idle.CancelAfter(IdleWindow);

            try
            {
                return await reader.ReadAsync(idle.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Write, flush, then ack — the whole delivery guarantee. The ack runs on
    /// CancellationToken.None on purpose: once the message is on stdout, a Ctrl-C that
    /// skipped the ack would hand the user a silent duplicate on the next run.
    /// </summary>
    private static async Task Emit(MessageWriter writer, IChannel channel, Message message, ulong deliveryTag, bool requeue)
    {
        await writer.WriteAsync(message, CancellationToken.None);
        await writer.FlushAsync(CancellationToken.None);

        if (!requeue)
        {
            await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
        }
    }

    private sealed record Delivery(Message Message, ulong DeliveryTag);

    /// <summary>
    /// The sanctioned adapter between the non-blocking delivery callback and the sequential
    /// loop. The body buffer is rented and reused after this returns, so the message is
    /// built here rather than handed on as memory.
    /// </summary>
    private sealed class BufferingConsumer(IChannel channel, ChannelWriter<Delivery> writer)
        : AsyncDefaultBasicConsumer(channel)
    {
        public override async Task HandleBasicDeliverAsync(
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
        {
            var message = Message.FromBytes(body.Span, Amqp.ToProperties(properties), exchange, routingKey, redelivered);

            try
            {
                await writer.WriteAsync(new Delivery(message, deliveryTag), cancellationToken);
            }
            catch (ChannelClosedException)
            {
                // The loop finished first. The broker requeues what it never got acked.
            }
        }

        // Without these the loop would wait out the idle window — or, under --follow,
        // forever — after the queue is deleted or the connection drops.
        public override Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken = default)
        {
            writer.TryComplete();
            return base.HandleBasicCancelAsync(consumerTag, cancellationToken);
        }

        public override Task HandleChannelShutdownAsync(object channel, RabbitMQ.Client.Events.ShutdownEventArgs reason)
        {
            writer.TryComplete();
            return base.HandleChannelShutdownAsync(channel, reason);
        }
    }
}
