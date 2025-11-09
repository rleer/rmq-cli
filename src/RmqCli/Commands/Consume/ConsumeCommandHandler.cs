using System.CommandLine;
using RabbitMQ.Client.Exceptions;
using RmqCli.Core.Models;
using RmqCli.Shared;
using RmqCli.Shared.Factories;
using RmqCli.Shared.Output;

namespace RmqCli.Commands.Consume;

public class ConsumeCommandHandler : ICommandHandler
{
    private readonly ServiceFactory _serviceFactory;

    public ConsumeCommandHandler(ServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
    }

    public void Configure(RootCommand rootCommand)
    {
        var description = """
                          Consume messages from a queue via the AMQP push API by registering a consumer to the queue. 
                          This follows the recommended way of consuming messages in RabbitMQ.

                          By default, messages are acknowledged after they are consumed. You can change the acknowledgment mode using the --ack-mode option.

                          Note: Unacknowledged messages will be marked as redelivered by RabbitMQ!

                          OUTPUT:
                            Messages can be printed to standard output (STDOUT) or saved to a file using the --to-file option.
                            Diagnostic information is written to standard error (STDERR).
                          
                          EXAMPLES:
                            rmq consume --queue my-queue
                            rmq consume --queue my-queue --ack-mode requeue
                            rmq consume --queue my-queue --count 10
                            rmq consume --queue my-queue --to-file output.txt
                          """;

        var consumeCommand = new Command("consume", description);

        var queueOption = new Option<string>("--queue")
        {
            Description = "Queue name to consume messages from.",
            Aliases = { "-q" },
            Required = true
        };

        var ackModeOption = new Option<AckModes>("--ack-mode")
        {
            Description = "Message acknowledgment mode.",
            Aliases = { "-a" },
            DefaultValueFactory = _ => AckModes.Ack
        };

        var countOption = new Option<int>("--count")
        {
            Description = "Number of messages to consume. Default is -1 (continuous consumption).",
            Aliases = { "-c" },
            DefaultValueFactory = _ => -1
        };

        var prefetchCountOption = new Option<ushort?>("--prefetch-count")
        {
            Description =
                "Number of unacknowledged messages the consumer can receive at once (QoS). Default: 100. Automatically set to 0 (unlimited) when using --ack-mode requeue.",
            Aliases = { "-p" }
        };

        var outputFormatOption = new Option<OutputFormat>("--output")
        {
            Description = "Output format",
            Aliases = { "-o" },
            DefaultValueFactory = _ => OutputFormat.Table
        };
        outputFormatOption.AcceptOnlyFromAmong("plain", "table", "json");

        var outputFileOption = new Option<string>("--to-file")
        {
            Description = "Path to output file to save consumed messages. The MessagesPerFile config option controls the number of messages per file before rotating.",
        };

        var compactOption = new Option<bool>("--compact")
        {
            Description = "Use compact format (only show properties with values). Applies to plain and table output formats.",
            DefaultValueFactory = _ => false
        };

        consumeCommand.Options.Add(queueOption);
        consumeCommand.Options.Add(ackModeOption);
        consumeCommand.Options.Add(countOption);
        consumeCommand.Options.Add(prefetchCountOption);
        consumeCommand.Options.Add(outputFileOption);
        consumeCommand.Options.Add(compactOption);
        consumeCommand.Options.Add(outputFormatOption);

        consumeCommand.Validators.Add(result =>
        {
            if (result.GetValue(queueOption) is null)
            {
                result.AddError("You must specify a queue to consume messages from.");
            }

            if (result.GetValue(outputFileOption) is { } filePath && !PathValidator.IsValidFilePath(filePath))
            {
                result.AddError($"The specified output file '{filePath}' is not valid.");
            }

            // Reject explicit prefetch-count with ack-mode requeue
            if (result.GetValue(ackModeOption) == AckModes.Requeue &&
                result.GetValue(prefetchCountOption).HasValue)
            {
                result.AddError("Cannot use --prefetch-count with --ack-mode requeue. " +
                                "This combination causes an infinite loop as requeued messages are immediately re-delivered. " +
                                "If you want to inspect messages without removing them, omit --prefetch-count (it will be set to 0 automatically) " +
                                "or use 'rmq peek' command instead.");
            }
        });

        consumeCommand.SetAction(Handle);

        rootCommand.Subcommands.Add(consumeCommand);
    }

    private async Task<int> Handle(ParseResult parseResult, CancellationToken cancellationToken)
    {
        // Create service (ServiceFactory extracts options from ParseResult)
        var consumeService = _serviceFactory.CreateConsumeService(parseResult);

        var cts = CancellationHelper.LinkWithCtrlCHandler(cancellationToken);

        try
        {
            return await consumeService.ConsumeMessages(cts.Token);
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