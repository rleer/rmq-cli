using System.CommandLine;
using RmqCli.Shared;

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
                           Consume messages from a queue. Warning: getting messages from a queue is a destructive action!

                           By default, messages are acknowledged after they are consumed. You can change the acknowledgment mode using the --ack-mode option.

                           Consumed messages can be printed to standard output (STDOUT) or saved to a file using the --to-file option.

                           Example usage:
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

        var outputFileOption = new Option<string>("--to-file")
        {
            Description = "Path to output file to save consumed messages. If not specified, messages will be printed to standard output (STDOUT).",
        };

        var compactOption = new Option<bool>("--compact")
        {
            Description = "Use compact table format (only show properties with values). Only applies to table output format.",
            DefaultValueFactory = _ => false
        };

        consumeCommand.Options.Add(queueOption);
        consumeCommand.Options.Add(ackModeOption);
        consumeCommand.Options.Add(countOption);
        consumeCommand.Options.Add(outputFileOption);
        consumeCommand.Options.Add(compactOption);

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
        catch (OperationCanceledException)
        {
            // Cancellation already handled
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}