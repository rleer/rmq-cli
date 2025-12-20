using System.CommandLine;
using RmqCli.Shared;
using RmqCli.Shared.Factories;
using RmqCli.Shared.Output;

namespace RmqCli.Commands.Purge;

public class PurgeCommandHandler : ICommandHandler
{
    private readonly ServiceFactory _serviceFactory;

    public PurgeCommandHandler(ServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
    }

    public void Configure(RootCommand rootCommand)
    {
        const string description = """
                                   Purge all ready messages from a queue.

                                   This operation uses the RabbitMQ Management API to delete all messages
                                   in the 'ready' state. Messages that are unacknowledged will not be affected.

                                   WARNING: This operation is destructive and cannot be undone.

                                   EXAMPLES:
                                     # Purge a queue
                                     rmq purge orders
                                     
                                     # Skip confirmation prompt
                                     rmq purge orders --force

                                     # Purge a queue in a specific vhost
                                     rmq purge orders --vhost production
                                     
                                     # Purge a queue via API endpoint
                                     rmq purge orders --use-api
                                   """;

        var purgeCommand = new Command("purge", description);

        var queueArgument = new Argument<string>("queue")
        {
            Description = "The name of the queue to purge.",
            Arity = ArgumentArity.ExactlyOne
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Force purge without confirmation prompt.",
            DefaultValueFactory = _ => false
        };

        var outputFormatOption = new Option<OutputFormat>("--output")
        {
            Description = "Output format",
            Aliases = { "-o" },
            DefaultValueFactory = _ => OutputFormat.Plain
        };
        outputFormatOption.AcceptOnlyFromAmong("plain", "json");
        
        var useApiOption = new Option<bool>("--use-api")
        {
            Description = "Use RabbitMQ Management API for purging the queue.",
            DefaultValueFactory = _ => false
        };

        purgeCommand.Arguments.Add(queueArgument);
        purgeCommand.Options.Add(forceOption);
        purgeCommand.Options.Add(outputFormatOption);
        purgeCommand.Options.Add(useApiOption);

        purgeCommand.SetAction(Handle);

        rootCommand.Subcommands.Add(purgeCommand);
    }

    private async Task<int> Handle(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var purgeService = _serviceFactory.CreatePurgeService(parseResult);

        var cts = CancellationHelper.LinkWithCtrlCHandler(cancellationToken);

        try
        {
            return await purgeService.PurgeQueueAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}