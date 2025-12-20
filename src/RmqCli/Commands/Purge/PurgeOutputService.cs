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
        _console = AnsiConsoleFactory.CreateStderrConsole(outputOptions.NoColor);
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
        var style = _outputOptions.NoColor ? Style.Plain : Style.Parse("orange1");

        var message = new List<Text> { new($"{Constants.SuccessSymbol} Purged ", Style.Plain) };

        if (response.PurgedMessages > 0)
        {
            message.Add(new Text($"{response.PurgedMessages} ", style));   
        }
        message.Add(new Text("messages from queue ", Style.Plain));
        message.Add(new Text(response.Queue, style));
        message.Add(new Text(" in vhost ", Style.Plain));
        message.Add(new Text(response.Vhost, style));
        message.Add(new Text(" successfully", Style.Plain));

        foreach (var text in message)
        {
            _console.Write(text);
        }
        
        _console.WriteLine();
    }

    private void WriteAsJson(PurgeResponse response)
    {
        var json = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.PurgeResponse);
        Console.Error.WriteLine(json);
    }
}