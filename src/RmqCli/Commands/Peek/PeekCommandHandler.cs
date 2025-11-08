using System.CommandLine;
using RabbitMQ.Client.Exceptions;
using RmqCli.Shared;
using RmqCli.Shared.Factories;
using RmqCli.Shared.Output;

namespace RmqCli.Commands.Peek;

public class PeekCommandHandler : ICommandHandler
{
    private readonly ServiceFactory _serviceFactory;

    public PeekCommandHandler(ServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
    }

    public void Configure(RootCommand rootCommand)
    {
        var description = """
                          Peek (inspect) messages from a queue without removing them.

                          Messages are fetched using polling (basic.get) and automatically requeued (un'acked).
                          This is useful when you want to limit the number of messages in-flight and avoid overwhelming the client.

                          Note: Un'acknowledged messages will be marked as redelivered by RabbitMQ!

                          Warning: Fetching messages one by one is highly discouraged as it is very inefficient compared to regular long-lived consumers. 
                          As with any polling-based algorithm, it will be extremely wasteful in systems where message publishing is sporadic and queues 
                          can stay empty for prolonged periods of time.

                          Example usage:
                            rmq peek --queue my-queue
                            rmq peek --queue my-queue --count 10
                            rmq peek --queue my-queue --to-file inspect.txt
                            rmq peek --queue my-queue --output json
                          """;

        var peekCommand = new Command("peek", description);

        var queueOption = new Option<string>("--queue")
        {
            Description = "Queue name to peek messages from.",
            Aliases = { "-q" },
            Required = true
        };

        var countOption = new Option<int>("--count")
        {
            Description = "Number of messages to peek. Default is 1.",
            Aliases = { "-c" },
            DefaultValueFactory = _ => 1,
            CustomParser = result =>
            {
                if (!result.Tokens.Any())
                {
                    return 1; // Default
                }

                if (int.TryParse(result.Tokens.Single().Value, out var count))
                {
                    if (count < 1)
                    {
                        result.AddError("Must be greater than 0");
                    }
                    return count;
                }

                result.AddError("Not an int.");
                return 0; // Ignored.
            }
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
            Description = "Path to output file to save peeked messages. If not specified, messages will be printed to standard output (STDOUT).",
        };

        var compactOption = new Option<bool>("--compact")
        {
            Description = "Use compact format (only show properties with values). Applies to plain and table output formats.",
            DefaultValueFactory = _ => false
        };

        peekCommand.Options.Add(queueOption);
        peekCommand.Options.Add(countOption);
        peekCommand.Options.Add(outputFileOption);
        peekCommand.Options.Add(compactOption);
        peekCommand.Options.Add(outputFormatOption);

        peekCommand.Validators.Add(result =>
        {
            if (result.GetValue(queueOption) is null)
            {
                result.AddError("You must specify a queue to peek messages from.");
            }

            if (result.GetValue(outputFileOption) is { } filePath && !PathValidator.IsValidFilePath(filePath))
            {
                result.AddError($"The specified output file '{filePath}' is not valid.");
            }
        });

        peekCommand.SetAction(Handle);

        rootCommand.Subcommands.Add(peekCommand);
    }

    private async Task<int> Handle(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var peekService = _serviceFactory.CreatePeekService(parseResult);

        var cts = CancellationHelper.LinkWithCtrlCHandler(cancellationToken);

        try
        {
            return await peekService.PeekMessages(cts.Token);
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