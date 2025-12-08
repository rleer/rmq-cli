using System.Text.Json;
using RmqCli.Shared.Json;
using RmqCli.Shared.Output;
using Spectre.Console;
using AnsiConsoleFactory = RmqCli.Shared.Factories.AnsiConsoleFactory;

namespace RmqCli.Commands.Publish;

public interface IPublishOutputService
{
    void WritePublishResult(PublishResponse response);
}

public class PublishOutputService : IPublishOutputService
{
    private readonly OutputOptions _outputOptions;
    private readonly IAnsiConsole _console;

    public PublishOutputService(OutputOptions outputOptions)
    {
        _outputOptions = outputOptions;
        _console = AnsiConsoleFactory.CreateStderrConsole(outputOptions.NoColor);
    }

    public void WritePublishResult(PublishResponse response)
    {
        if (_outputOptions.Quiet)
            return;

        if (_outputOptions.Format == OutputFormat.Json)
        {
            WritePublishResultInJsonFormat(response);
        }
        else
        {
            WritePublishResultInPlainFormat(response);
        }
    }

    private void WritePublishResultInJsonFormat(PublishResponse response)
    {
        var resultJson = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.PublishResponse);
        Console.Error.WriteLine(resultJson);
    }

    private void WritePublishResultInPlainFormat(PublishResponse response)
    {
        var rows = new List<Text>();
        var style = _outputOptions.NoColor ? Style.Plain : Style.Parse("dim");

        if (response.Destination is { } dest)
        {
            if (dest.Queue is not null)
            {
                rows.Add(new Text($"  Queue:       {dest.Queue}", style));
            }
            else if (dest is { Exchange: not null, RoutingKey: not null })
            {
                rows.Add(new Text($"  Exchange:    {dest.Exchange}", style));
                rows.Add(new Text($"  Routing Key: {dest.RoutingKey}", style));
            }
        }

        if (response.Result is { } result)
        {
            if (response.Result.MessagesPublished > 1)
            {
                rows.Add(new Text($"  Message IDs: {result.FirstMessageId} → {result.LastMessageId}", style));
                rows.Add(new Text($"  Size:        {result.AverageMessageSize} avg. ({result.TotalSize} total)", style));
                rows.Add(new Text($"  Time:        {result.FirstTimestamp} UTC → {result.LastTimestamp} UTC", style));
            }
            else
            {
                rows.Add(new Text($"  Message ID:  {result.FirstMessageId}", style));
                rows.Add(new Text($"  Size:        {result.TotalSize}", style));
                rows.Add(new Text($"  Timestamp:   {result.FirstTimestamp} UTC", style));
            }
        }

        _console.Write(new Rows(rows));
    }
}