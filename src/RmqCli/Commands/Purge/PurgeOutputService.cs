using System.Text.Json;
using RmqCli.Shared;
using RmqCli.Shared.Json;
using RmqCli.Shared.Output;
using Spectre.Console;
using AnsiConsoleFactory = RmqCli.Shared.Factories.AnsiConsoleFactory;

namespace RmqCli.Commands.Purge;

public interface IPurgeOutputService
{
    void Write(PurgeResponse response);
}

public class PurgeOutputService : IPurgeOutputService
{
    private readonly OutputOptions _outputOptions;
    private readonly IAnsiConsole _console;

    public PurgeOutputService(OutputOptions outputOptions)
    {
        _outputOptions = outputOptions;
        _console = AnsiConsoleFactory.CreateStderrConsole();
    }

    public void Write(PurgeResponse response)
    {
        if (_outputOptions.Quiet)
            return;

        if (_outputOptions.Format is OutputFormat.Json)
        {
            WriteAsJson(response);
        }
        else
        {
            WriteAsFormattedText(response);
        }
    }

    private void WriteAsFormattedText(PurgeResponse response)
    {
        var formattedQueueName = _outputOptions.NoColor ? response.Queue : $"[orange1]{response.Queue}[/]";
        var formattedVhostName = _outputOptions.NoColor ? response.Vhost : $"[orange1]{response.Vhost}[/]";

        _console.MarkupLine($"{Constants.SuccessSymbol} Queue {formattedQueueName} in vhost {formattedVhostName} purged successfully");
    }

    private void WriteAsJson(PurgeResponse response)
    {
        var json = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.PurgeResponse);
        Console.Error.WriteLine(json);
    }
}