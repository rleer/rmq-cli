using System.CommandLine;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RmqCli.Shared;

namespace RmqCli.Commands.Publish;

public class PublishCommandHandler : ICommandHandler
{
    private readonly ServiceFactory _serviceFactory;

    public PublishCommandHandler(ServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
    }

    public void Configure(RootCommand rootCommand)
    {
        const string description = """
                                   Publish messages to a queue or via exchange and routing-key.

                                   INPUT MODES:
                                     --body:          Specify message body directly
                                     --from-file:     Read plain text messages from file (delimiter-separated)
                                     STDIN:           Pipe/redirect plain text messages
                                     --message:       Provide complete message as JSON (with properties/headers)
                                     --message-file:  Read JSON message(s) from file (supports NDJSON)
                                     --json-format:   Treat STDIN as newline-delimited JSON messages

                                   MESSAGE PROPERTIES:
                                     Set RabbitMQ message properties using dedicated flags:
                                       --app-id, --content-type, --correlation-id, --priority, --reply-to, etc.

                                     Add custom headers using --header or -H (repeatable):
                                       rmq publish -q orders --body "order" -H "x-tenant:acme" -H "x-trace-id:123"

                                   JSON MESSAGE FORMAT:
                                     Provide complete messages in JSON format:
                                       rmq publish -q orders --message '{"body":"data","properties":{"priority":5}}'

                                     CLI flags override JSON properties for flexibility:
                                       rmq publish -q orders --message-file msg.json --priority 9

                                   EXAMPLES:
                                     # Simple message
                                     rmq publish -q my-queue --body "Hello, World!"

                                     # With properties
                                     rmq publish -q orders --body "order" --priority 5 --content-type application/json

                                     # With custom headers
                                     rmq publish -q orders --body "order" -H "x-tenant:acme" -H "x-trace-id:123"

                                     # JSON message
                                     rmq publish -q orders --message '{"body":"order","properties":{"priority":5}}'

                                     # Batch NDJSON
                                     rmq publish -q orders --message-file batch.ndjson

                                     # RPC pattern
                                     rmq publish -q rpc --body "calc" --correlation-id req-123 --reply-to callback

                                   Note: Messages are sent with the mandatory flag set to true, meaning that if the message
                                   cannot be routed to any queue, it will be returned to the sender.
                                   """;

        var publishCommand = new Command("publish", description);

        var queueOption = new Option<string>("--queue")
        {
            Description = "Queue name to send message to.",
            Aliases = { "-q" }
        };

        var exchangeOption = new Option<string>("--exchange")
        {
            Description = "Exchange name to send message to.",
            Aliases = { "-e" }
        };

        var routingKeyOption = new Option<string>("--routing-key")
        {
            Description = "Routing key to use when sending message via exchange.",
            Aliases = { "-r" }
        };

        var messageOption = new Option<string>("--body")
        {
            Description = "Message body to send.",
            Aliases = { "-m" }
        };

        var fromFileOption = new Option<string>("--from-file")
        {
            Description = "Path to file that contains message bodies to send."
        };

        var burstOption = new Option<int>("--burst")
        {
            Description = "Send each message multiple times (burst mode). Default is no burst.",
            Aliases = { "-b" },
            DefaultValueFactory = _ => 1
        };

        var outputFormatOption = new Option<OutputFormat>("--output")
        {
            Description = "Output format",
            Aliases = { "-o" },
            DefaultValueFactory = _ => OutputFormat.Plain
        };
        outputFormatOption.AcceptOnlyFromAmong("plain", "json");

        // Message property options
        var appIdOption = new Option<string>("--app-id")
        {
            Description = "Application ID that produced the message",
            Aliases = { "-a" }
        };

        var contentTypeOption = new Option<string>("--content-type")
        {
            Description = "MIME content type (e.g., application/json, text/plain)",
            Aliases = { "-ct" }
        };

        var contentEncodingOption = new Option<string>("--content-encoding")
        {
            Description = "MIME content encoding (e.g., gzip, identity)",
            Aliases = { "-ce" }
        };

        var correlationIdOption = new Option<string>("--correlation-id")
        {
            Description = "Correlation ID for request/reply patterns",
            Aliases = { "-cid" }
        };

        var deliveryModeOption = new Option<DeliveryModes?>("--delivery-mode")
        {
            Description = "Delivery mode: Transient (non-persistent) or Persistent",
            Aliases = { "-dm" }
        };

        var expirationOption = new Option<string>("--expiration")
        {
            Description = "Message expiration time in milliseconds",
            Aliases = { "-exp" }
        };

        var priorityOption = new Option<int?>("--priority")
        {
            Description = "Message priority (0-255, where 255 is highest)",
            Aliases = { "-p" }
        };

        var replyToOption = new Option<string>("--reply-to")
        {
            Description = "Queue name for replies in RPC patterns",
            Aliases = { "-rt" }
        };

        var typeOption = new Option<string>("--type")
        {
            Description = "Message type name for application use",
            Aliases = { "-t" }
        };

        var userIdOption = new Option<string>("--user-id")
        {
            Description = "User ID (must match authenticated user)",
            Aliases = { "-u" }
        };

        var headerOption = new Option<string[]>("--header")
        {
            Description = "Custom header in format 'key:value'. Can be specified multiple times.",
            Aliases = { "-H" },
            AllowMultipleArgumentsPerToken = true
        };

        // JSON message options
        var jsonMessageOption = new Option<string>("--message")
        {
            Description = "Complete message as JSON with body and optional properties",
            Aliases = { "-msg" }
        };

        var messageFileOption = new Option<string>("--message-file")
        {
            Description = "Path to file containing JSON message(s). Supports NDJSON format.",
            Aliases = { "-mf" }
        };

        var jsonFormatOption = new Option<bool>("--json-format")
        {
            Description = "Treat STDIN input as newline-delimited JSON messages",
            DefaultValueFactory = _ => false
        };

        publishCommand.Options.Add(queueOption);
        publishCommand.Options.Add(exchangeOption);
        publishCommand.Options.Add(routingKeyOption);
        publishCommand.Options.Add(messageOption);
        publishCommand.Options.Add(fromFileOption);
        publishCommand.Options.Add(burstOption);
        publishCommand.Options.Add(outputFormatOption);
        publishCommand.Options.Add(appIdOption);
        publishCommand.Options.Add(contentTypeOption);
        publishCommand.Options.Add(contentEncodingOption);
        publishCommand.Options.Add(correlationIdOption);
        publishCommand.Options.Add(deliveryModeOption);
        publishCommand.Options.Add(expirationOption);
        publishCommand.Options.Add(priorityOption);
        publishCommand.Options.Add(replyToOption);
        publishCommand.Options.Add(typeOption);
        publishCommand.Options.Add(userIdOption);
        publishCommand.Options.Add(headerOption);
        publishCommand.Options.Add(jsonMessageOption);
        publishCommand.Options.Add(messageFileOption);
        publishCommand.Options.Add(jsonFormatOption);

        publishCommand.Validators.Add(result =>
        {
            // Destination validation
            if (result.GetValue(queueOption) is null &&
                (result.GetValue(routingKeyOption) is null || result.GetValue(exchangeOption) is null))
            {
                result.AddError("You must specify a queue or both an exchange and a routing key.");
            }

            if (result.GetValue(queueOption) is { } queue && string.IsNullOrEmpty(queue))
            {
                result.AddError("Queue name cannot be empty.");
            }

            if (result.GetValue(exchangeOption) is { } exchange && string.IsNullOrEmpty(exchange))
            {
                result.AddError("Exchange name cannot be empty. Consider using the --queue option if you want to send messages directly to a queue.");
            }

            if (result.GetValue(routingKeyOption) is { } routingKey && string.IsNullOrEmpty(routingKey))
            {
                result.AddError("Routing key cannot be empty. Consider using the --queue option if you want to send messages directly to a queue.");
            }

            // Input mode validation
            var hasBody = result.GetValue(messageOption) is not null;
            var hasFromFile = result.GetValue(fromFileOption) is not null;
            var hasJsonMessage = result.GetValue(jsonMessageOption) is not null;
            var hasMessageFile = result.GetValue(messageFileOption) is not null;
            var hasJsonFormat = result.GetValue(jsonFormatOption);
            var hasStdin = Console.IsInputRedirected;

            if (!hasBody && !hasFromFile && !hasJsonMessage && !hasMessageFile && !hasStdin)
            {
                result.AddError("You must specify a message using --body, --from-file, --message, --message-file, or pipe/redirect via STDIN.");
            }

            // Prevent mixing incompatible input modes
            if ((hasJsonMessage || hasMessageFile) && hasBody)
            {
                result.AddError("Cannot specify both --message/--message-file and --body. Use --message for JSON format or --body for plain text.");
            }

            if ((hasJsonMessage || hasMessageFile) && hasFromFile)
            {
                result.AddError("Cannot specify both --message/--message-file and --from-file.");
            }

            if (hasJsonFormat && (hasBody || hasFromFile))
            {
                result.AddError("Cannot use --json-format with --body or --from-file.");
            }

            if (hasJsonMessage && hasMessageFile)
            {
                result.AddError("Cannot specify both --message and --message-file.");
            }

            if (hasFromFile && hasBody)
            {
                result.AddError("You cannot specify both --body and --from-file.");
            }

            // File existence validation
            if (result.GetValue(fromFileOption) is { } filePath && !File.Exists(filePath))
            {
                result.AddError($"Input file '{filePath}' not found.");
            }

            if (result.GetValue(messageFileOption) is { } msgFilePath && !File.Exists(msgFilePath))
            {
                result.AddError($"Message file '{msgFilePath}' not found.");
            }

            // Property validation
            if (result.GetValue(priorityOption) is { } priority && (priority <= 0 || priority >= 255))
            {
                result.AddError("Priority must be between 0 and 255.");
            }

            // Header format validation
            if (result.GetValue(headerOption) is { } headers)
            {
                foreach (var header in headers)
                {
                    if (!header.Contains(':'))
                    {
                        result.AddError($"Invalid header format: '{header}'. Expected 'key:value'.");
                    }
                }
            }
        });

        publishCommand.SetAction(Handle);

        rootCommand.Subcommands.Add(publishCommand);
    }

    private async Task<int> Handle(ParseResult parseResult, CancellationToken cancellationToken)
    {
        // Create service (ServiceFactory extracts OutputOptions from ParseResult)
        var publishService = _serviceFactory.CreatePublishService(parseResult);

        // Extract publish-specific options
        var options = ServiceFactory.CreatePublishOptions(parseResult);

        var cts = CancellationHelper.LinkWithCtrlCHandler(cancellationToken);

        try
        {
            // Route to appropriate method based on input source
            if (options.InputFile is not null)
            {
                return await publishService.PublishMessageFromFile(
                    options.Destination,
                    options.InputFile,
                    options.BurstCount,
                    cts.Token);
            }

            if (options.IsStdinRedirected)
            {
                return await publishService.PublishMessageFromStdin(
                    options.Destination,
                    options.BurstCount,
                    cts.Token);
            }

            return await publishService.PublishMessage(
                options.Destination,
                [options.MessageBody ?? string.Empty],
                options.BurstCount,
                cts.Token);
        }
        catch (Exception e)
        {
            switch (e)
            {
                case OperationCanceledException:
                    // Cancellation already handled
                    return 0;
                case BrokerUnreachableException or RabbitMQClientException:
                    // RabbitMQ connection issues already handled
                    return 1;
                default:
                    Console.Error.WriteLine(e);
                    return 1;
            }
        }
    }
}