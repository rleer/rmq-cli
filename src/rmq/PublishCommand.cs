using System.CommandLine;
using RabbitMQ.Client;

namespace Rmq;

public static class PublishCommand
{
    public static Command Create()
    {
        var queue = new Option<string?>("--queue", "-q")
        {
            Description = "Publish to this queue via the default exchange"
        };

        var exchange = new Option<string?>("--exchange", "-e")
        {
            Description = "Publish to this exchange"
        };

        var routingKey = new Option<string?>("--routing-key", "-r")
        {
            Description = "Routing key for --exchange. Defaults to the routingKey in the message, then empty."
        };

        var body = new Option<string?>("--body")
        {
            Description = "Message body, taken literally"
        };

        var message = new Option<string?>("--message")
        {
            Description = "One message as JSON, in the shape consume emits"
        };

        var messageFile = new Option<string?>("--message-file")
        {
            Description = "File of NDJSON messages, one per line"
        };

        var header = new Option<string[]>("--header", "-H")
        {
            Description = "Header as key:value, repeatable. Values are typed by inspection: 3 is a number, true is a boolean.",
            AllowMultipleArgumentsPerToken = false
        };

        var contentType = new Option<string?>("--content-type") { Description = "Content type, e.g. application/json" };
        var contentEncoding = new Option<string?>("--content-encoding") { Description = "Content encoding, e.g. gzip" };
        var persistent = new Option<bool>("--persistent") { Description = "Mark messages persistent (delivery mode 2)" };
        var priority = new Option<byte?>("--priority") { Description = "Priority, 0-255" };
        var correlationId = new Option<string?>("--correlation-id") { Description = "Correlation ID" };
        var replyTo = new Option<string?>("--reply-to") { Description = "Reply-to queue or address" };
        var expiration = new Option<string?>("--expiration") { Description = "Per-message TTL in milliseconds, as a string" };
        var messageId = new Option<string?>("--message-id") { Description = "Message ID" };
        var timestamp = new Option<long?>("--timestamp") { Description = "Timestamp in Unix seconds" };
        var type = new Option<string?>("--type") { Description = "Message type name" };
        var userId = new Option<string?>("--user-id") { Description = "User ID. The broker rejects a value that is not the connecting user." };
        var appId = new Option<string?>("--app-id") { Description = "Application ID" };

        var command = new Command("publish", """
            Publish messages to a queue or an exchange.

            The body comes from --body, --message, --message-file, or STDIN. STDIN and
            --message-file are NDJSON in exactly the shape consume writes, so this composes:

              rmq consume -q source --url amqp://a/ | rmq publish -q dest --url amqp://b/

            Property flags override the same field in the JSON, per field. The destination
            always comes from --queue or --exchange, never from the message.

            Publishing to a --queue that does not exist is an error. Publishing to an
            --exchange that routes nowhere is not. That holds on both transports.
            """);

        command.Add(queue);
        command.Add(exchange);
        command.Add(routingKey);
        command.Add(body);
        command.Add(message);
        command.Add(messageFile);
        command.Add(header);
        command.Add(contentType);
        command.Add(contentEncoding);
        command.Add(persistent);
        command.Add(priority);
        command.Add(correlationId);
        command.Add(replyTo);
        command.Add(expiration);
        command.Add(messageId);
        command.Add(timestamp);
        command.Add(type);
        command.Add(userId);
        command.Add(appId);

        command.Validators.Add(result =>
        {
            var toQueue = result.GetValue(queue) != null;
            var toExchange = result.GetValue(exchange) != null;

            if (toQueue && toExchange)
            {
                result.AddError("--queue and --exchange are mutually exclusive");
            }
            else if (!toQueue && !toExchange)
            {
                result.AddError("a destination is required: --queue, or --exchange with --routing-key");
            }

            if (toQueue && result.GetValue(routingKey) != null)
            {
                result.AddError("--routing-key applies to --exchange; --queue is already the routing key");
            }

            var sources = new[] { result.GetValue(body), result.GetValue(message), result.GetValue(messageFile) }
                .Count(value => value != null);

            if (sources > 1)
            {
                result.AddError("--body, --message, and --message-file are mutually exclusive");
            }

            // Reading a terminal would just hang looking like a crash.
            if (sources == 0 && !Console.IsInputRedirected)
            {
                result.AddError("no message source: pass --body, --message, --message-file, or pipe NDJSON on STDIN");
            }
        });

        command.SetAction((parse, ct) => Run(
            GlobalOptions.Settings(parse),
            parse.GetValue(queue),
            parse.GetValue(exchange),
            parse.GetValue(routingKey),
            parse.GetValue(body),
            parse.GetValue(message),
            parse.GetValue(messageFile),
            new MessageProperties
            {
                ContentType = parse.GetValue(contentType),
                ContentEncoding = parse.GetValue(contentEncoding),
                DeliveryMode = parse.GetValue(persistent) ? DeliveryModes.Persistent : null,
                Priority = parse.GetValue(priority),
                CorrelationId = parse.GetValue(correlationId),
                ReplyTo = parse.GetValue(replyTo),
                Expiration = parse.GetValue(expiration),
                MessageId = parse.GetValue(messageId),
                Timestamp = parse.GetValue(timestamp),
                Type = parse.GetValue(type),
                UserId = parse.GetValue(userId),
                AppId = parse.GetValue(appId)
            },
            HeaderParser.Parse(parse.GetValue(header) ?? []),
            ct));

        return command;
    }

    private static async Task<int> Run(
        ConnectionSettings settings,
        string? queue,
        string? exchange,
        string? routingKey,
        string? body,
        string? messageJson,
        string? messageFile,
        MessageProperties cliProperties,
        Dictionary<string, object> cliHeaders,
        CancellationToken ct)
    {
        if (settings.Transport == Transport.Http)
        {
            return await RunOverHttp(settings, queue, exchange, routingKey, body, messageJson, messageFile, cliProperties, cliHeaders, ct);
        }

        await using var connection = await Amqp.ConnectAsync(settings, ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // No publisher confirmations. Confirmation *tracking* is the only way to await them
        // per publish, and it stamps x-dotnet-pub-seq-no onto every message — which would
        // break the properties round-trip this tool is built around. The passive declare
        // below catches the failure that actually happens, a mistyped queue name, and costs
        // one round trip rather than one per message.
        if (queue != null)
        {
            await channel.QueueDeclarePassiveAsync(queue, ct);
        }

        var returned = 0;
        channel.BasicReturnAsync += (_, args) =>
        {
            returned++;
            Log.Error($"undeliverable: {args.ReplyText} (exchange '{args.Exchange}', routing key '{args.RoutingKey}')");
            return Task.CompletedTask;
        };

        var published = 0;
        await foreach (var line in Read(body, messageJson, messageFile, ct))
        {
            var merged = PropertyMerger.Merge(line, cliProperties, cliHeaders);

            // The destination is the command line's, never the message's — otherwise every
            // piped message would republish onto the exchange it was consumed from.
            var targetExchange = queue != null ? string.Empty : exchange!;
            var targetKey = queue ?? routingKey ?? merged.RoutingKey ?? string.Empty;

            await channel.BasicPublishAsync(
                targetExchange,
                targetKey,
                // --queue names a concrete queue, so a typo that routes nowhere is an error
                // worth reporting. --exchange is different: unroutable is ordinary there,
                // and a fanout with no bindings is not a mistake.
                mandatory: queue != null,
                basicProperties: Amqp.ToBasicProperties(merged.Properties),
                body: merged.BodyBytes,
                ct);

            published++;
            Log.Debug($"published to exchange='{targetExchange}' routingKey='{targetKey}'");
        }

        Console.Error.WriteLine($"published {published} message{(published == 1 ? "" : "s")}");

        // Returns arrive asynchronously, so this catches a batch that bounced rather than
        // the last message of one. A partly-bounced run must not report success.
        return returned > 0 ? ExitCode.Connection : ExitCode.Success;
    }

    /// <summary>
    /// The same publish, over the Management API. Written out rather than folded into the
    /// AMQP path: there is no channel, no passive declare, and routability comes back in
    /// the response instead of asynchronously on basic.return.
    /// </summary>
    private static async Task<int> RunOverHttp(
        ConnectionSettings settings,
        string? queue,
        string? exchange,
        string? routingKey,
        string? body,
        string? messageJson,
        string? messageFile,
        MessageProperties cliProperties,
        Dictionary<string, object> cliHeaders,
        CancellationToken ct)
    {
        using var client = Http.CreateClient(settings);

        var targetExchange = queue != null ? string.Empty : exchange!;
        var published = 0;
        var unroutable = 0;

        await foreach (var line in Read(body, messageJson, messageFile, ct))
        {
            var merged = PropertyMerger.Merge(line, cliProperties, cliHeaders);
            var targetKey = queue ?? routingKey ?? merged.RoutingKey ?? string.Empty;

            var routed = await Http.PublishAsync(client, settings, targetExchange, targetKey, merged, ct);

            // "routed" is exactly what mandatory reports over AMQP, so it counts the same
            // way: an error for --queue, ordinary for --exchange.
            if (!routed && queue != null)
            {
                unroutable++;
                Log.Error($"undeliverable: NO_ROUTE (exchange '{targetExchange}', routing key '{targetKey}')");
            }

            published++;
            Log.Debug($"published to exchange='{targetExchange}' routingKey='{targetKey}'");
        }

        Console.Error.WriteLine($"published {published} message{(published == 1 ? "" : "s")}");

        return unroutable > 0 ? ExitCode.Connection : ExitCode.Success;
    }

    /// <summary>
    /// The four sources, in the order the flags are checked. STDIN and --message-file
    /// stream a line at a time so a long consume pipe does not buffer.
    /// </summary>
    private static async IAsyncEnumerable<Message> Read(
        string? body,
        string? messageJson,
        string? messageFile,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (body != null)
        {
            yield return new Message { Body = body };
            yield break;
        }

        if (messageJson != null)
        {
            yield return MessageJson.Parse(messageJson);
            yield break;
        }

        if (messageFile != null)
        {
            using var file = new StreamReader(messageFile);
            await foreach (var message in MessageJson.ReadLinesAsync(file, ct))
            {
                yield return message;
            }

            yield break;
        }

        using var input = new StreamReader(Console.OpenStandardInput());
        await foreach (var message in MessageJson.ReadLinesAsync(input, ct))
        {
            yield return message;
        }
    }
}
