using System.CommandLine;
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

                                   Messages can be specified 
                                     - directly via the --body option
                                     - read from a file using the --from-file option
                                     - piped from standard input (STDIN)

                                   Multiple messages can be sent from a file or STDIN. Messages will be separated by the configured 
                                   message delimiter (see config). Default delimiter is the OS specific newline character.

                                   At the moment, only the message body is supported for publishing. 
                                   For each message, the following RabbitMQ properties are set:
                                     - message_id: auto-generated (incremental)
                                     - timestamp: UTC timestamp of when the message was published 
                                                  (UTC because RabbitMQ uses Unix timestamp in seconds as the underlying format)

                                   Example usage:
                                     rmq publish --queue my-queue --body "Hello, World!"
                                     rmq publish --exchange my-exchange --routing-key my-routing-key --body "Hello, World!"
                                     rmq publish --from-file message.txt
                                     echo "Hello, World!" | rmq publish --queue my-queue
                                     rmq publish --queue my-queue --burst 10 --body "Burst message" > output.txt

                                   Note that messages are sent with the mandatory flag set to true, meaning that if the message 
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

        publishCommand.Options.Add(queueOption);
        publishCommand.Options.Add(exchangeOption);
        publishCommand.Options.Add(routingKeyOption);
        publishCommand.Options.Add(messageOption);
        publishCommand.Options.Add(fromFileOption);
        publishCommand.Options.Add(burstOption);

        publishCommand.Validators.Add(result =>
        {
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

            if (result.GetValue(messageOption) is null && result.GetValue(fromFileOption) is null && !Console.IsInputRedirected)
            {
                result.AddError("You must specify a message using --body, --from-file, or pipe/redirect messages via STDIN.");
            }

            if (result.GetValue(fromFileOption) is not null && result.GetValue(messageOption) is not null)
            {
                result.AddError("You cannot specify both a message and a file that contains the message body.");
            }

            if (result.GetValue(fromFileOption) is { } filePath && !File.Exists(filePath))
            {
                result.AddError($"Input file '{filePath}' not found.");
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
        catch (OperationCanceledException)
        {
            // Cancellation already handled
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}