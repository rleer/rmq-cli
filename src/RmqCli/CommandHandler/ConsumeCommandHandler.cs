using System.CommandLine;
using Microsoft.Extensions.Logging;
using RmqCli.Common;
using RmqCli.ConsumeCommand;

namespace RmqCli.CommandHandler;

public class ConsumeCommandHandler : ICommandHandler
{
    private readonly ILogger<ConsumeCommandHandler> _logger;
    private readonly IConsumeService _consumeService;

    public ConsumeCommandHandler(ILogger<ConsumeCommandHandler> logger, IConsumeService consumeService)
    {
        _logger = logger;
        _consumeService = consumeService;
    }

    public void Configure(RootCommand rootCommand)
    {
        _logger.LogDebug("Configuring consume command");

        var consumeCommand = new Command("consume", "Consume messages from a queue. Warning: getting messages from a queue is a destructive action!");

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
        _logger.LogDebug("Running handler for consume command");
        
        var queue = parseResult.GetRequiredValue<string>("--queue");
        var ackMode = parseResult.GetValue<AckModes>("--ack-mode");
        var messageCount = parseResult.GetValue<int>("--count");
        var outputFilePath = parseResult.GetValue<string>("--to-file");
        var outputFormat = parseResult.GetValue<OutputFormat>("--output");
 
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // Prevent the process from terminating immediately
            cts.Cancel();    // Signal cancellation
        };

        FileInfo? outputFileInfo = null;
        if (!string.IsNullOrWhiteSpace(outputFilePath))
        {
            outputFileInfo = new FileInfo(Path.GetFullPath(outputFilePath, Environment.CurrentDirectory));
        }

        await _consumeService.ConsumeMessages(queue, ackMode, outputFileInfo, messageCount, outputFormat, cts.Token); 

        _logger.LogDebug("Message consumer is done. Stopping application.");
    }
}