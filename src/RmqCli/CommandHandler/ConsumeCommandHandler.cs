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

        var queueOption = new Option<string>("--queue", "Queue name to consume messages from");
        queueOption.AddAlias("-q");
        queueOption.IsRequired = true;

        var ackModeOption = new Option<AckModes>("--ack-mode", "Acknowledgment mode");
        ackModeOption.AddAlias("-a");
        ackModeOption.SetDefaultValue(AckModes.Ack);

        var countOption = new Option<int>("--count", "Number of messages to consume (default: continuous consumption until interrupted)");
        countOption.AddAlias("-c");
        countOption.SetDefaultValue(-1);

        var outputFileOption = new Option<string>("--to-file", "Output file to write messages to. Or just pipe/redirect output to a file.");

        var outputFormatOption = new Option<OutputFormat>("--output", "Output format. One of: plain, table or json.");
        outputFormatOption.AddAlias("-o");
        outputFormatOption.SetDefaultValue(OutputFormat.Plain);
        
        consumeCommand.AddOption(queueOption);
        consumeCommand.AddOption(ackModeOption);
        consumeCommand.AddOption(countOption);
        consumeCommand.AddOption(outputFileOption);
        consumeCommand.AddOption(outputFormatOption);

        consumeCommand.AddValidator(result =>
        {
            if (result.GetValueForOption(queueOption) is null)
            {
                result.ErrorMessage = "You must specify a queue to consume messages from.";
            }

            if (result.GetValueForOption(outputFileOption) is { } filePath && !PathValidator.IsValidFilePath(filePath))
            {
                result.ErrorMessage = $"The specified output file '{filePath}' is not valid.";
            }
        });

        consumeCommand.SetHandler(Handle, queueOption, ackModeOption, countOption, outputFileOption, outputFormatOption);

        rootCommand.AddCommand(consumeCommand);
    }

    private async Task Handle(string queue, AckModes ackMode, int messageCount, string outputFilePath, OutputFormat outputFormat)
    {
        _logger.LogDebug("Running handler for consume command");
        
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