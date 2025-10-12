using System.CommandLine;
using RmqCli.Common;
using RmqCli.ConsumeCommand;

namespace RmqCli.CommandHandler;

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
        
        consumeCommand.Options.Add(queueOption);
        consumeCommand.Options.Add(ackModeOption);
        consumeCommand.Options.Add(countOption);
        consumeCommand.Options.Add(outputFileOption);

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

    private async Task Handle(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var consumeService = _serviceFactory.CreateConsumeService(parseResult);
        
        var queue = parseResult.GetRequiredValue<string>("--queue");
        var ackMode = parseResult.GetValue<AckModes>("--ack-mode");
        var messageCount = parseResult.GetValue<int>("--count");
        var outputFilePath = parseResult.GetValue<string>("--to-file");
        var outputFormat = parseResult.GetValue<OutputFormat>("--output");
 
        var cts = CancellationHelper.LinkWithCtrlCHandler(cancellationToken);

        FileInfo? outputFileInfo = null;
        if (!string.IsNullOrWhiteSpace(outputFilePath))
        {
            outputFileInfo = new FileInfo(Path.GetFullPath(outputFilePath, Environment.CurrentDirectory));
        }

        await consumeService.ConsumeMessages(queue, ackMode, outputFileInfo, messageCount, outputFormat, cts.Token); 
    }
}