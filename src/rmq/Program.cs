using System.CommandLine;

namespace Rmq;

/// <summary>
/// The whole object graph, constructed by hand. No DI container — it costs startup
/// time and hides the wiring, both of which this tool is specifically avoiding.
/// </summary>
public static class Program
{
    private const string Rabbit = """
                                    (\(\
                                    (-.-)
                                   o(")(")
                                  """;

    public static async Task<int> Main(string[] args)
    {
        var root = new RootCommand($"{Rabbit}\nDeveloper-focused CLI for publishing and consuming RabbitMQ messages");

        GlobalOptions.AddTo(root);
        root.Add(PublishCommand.Create());
        root.Add(ConsumeCommand.Create());
        root.Add(PurgeCommand.Create());

        var parse = root.Parse(args);

        // Parse failures are usage errors, and the contract says those exit 2 — not the 1
        // System.CommandLine would return on its own.
        if (parse.Errors.Count > 0)
        {
            foreach (var error in parse.Errors)
            {
                Log.Error(error.Message);
            }

            Console.Error.WriteLine("Try 'rmq --help' for usage.");
            return ExitCode.Usage;
        }

        Log.Verbose = parse.GetValue(GlobalOptions.Verbose);

        // One place to turn exceptions into exit codes, rather than the same three catch
        // blocks in every command.
        try
        {
            return await parse.InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
        }
        catch (BrokerException ex)
        {
            Log.Error(ex.Message, ex);
            return ExitCode.Connection;
        }
        catch (ArgumentException ex)
        {
            // Bad --url, bad --header, malformed NDJSON: user input that only fails once
            // the command runs.
            Log.Error(ex.Message, ex);
            return ExitCode.Usage;
        }
        catch (OperationCanceledException)
        {
            return ExitCode.Interrupted;
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex)
        {
            // A missing queue or a permissions failure closes the channel mid-operation.
            var reason = ex.ShutdownReason;
            Log.Error(reason != null ? $"{reason.ReplyText} (code {reason.ReplyCode})" : ex.Message, ex);
            return ExitCode.Connection;
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message, ex);
            return ExitCode.Connection;
        }
    }
}
