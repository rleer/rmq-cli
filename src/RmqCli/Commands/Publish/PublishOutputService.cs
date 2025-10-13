using System.Text.Json;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output.Formatters.Json;
using RmqCli.Shared;
using Spectre.Console;
using AnsiConsoleFactory = RmqCli.Shared.Factories.AnsiConsoleFactory;

namespace RmqCli.Commands.Publish;

public interface IPublishOutputService
{
    void WritePublishResult(PublishResponse response);
}

public class PublishOutputService : IPublishOutputService
{
    private readonly CliConfig _cliConfig;
    private readonly IAnsiConsole _console;

    public PublishOutputService(CliConfig cliConfig)
    {
        _cliConfig = cliConfig;
        _console = AnsiConsoleFactory.CreateStdoutConsole();
    }

    public void WritePublishResult(PublishResponse response)
    {
        if (_cliConfig.Format == OutputFormat.Json)
            WritePublishResultInJsonFormat(response);
        
        if (_cliConfig.Format == OutputFormat.Plain)
            WritePublishResultInPlainFormat(response);
    }
    
    private void WritePublishResultInJsonFormat(PublishResponse response)
    {
        var resultJson = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(PublishResponse)));
        Console.Out.WriteLine(resultJson);
    }
    
    private void WritePublishResultInPlainFormat(PublishResponse response)
    {
        if (response.Destination is { } dest)
        {
            if (dest.Queue is not null)
            {
                _console.MarkupLineInterpolated($"  Queue:       {dest.Queue}");
            }
            else if (dest is { Exchange: not null, RoutingKey: not null })
            {
                _console.MarkupLineInterpolated($"  Exchange:    {dest.Exchange}");
                _console.MarkupLineInterpolated($"  Routing Key: {dest.RoutingKey}");
            } 
        }

        if (response.Result is { } result)
        {
            if (response.Result.MessagesPublished > 1)
            {

                _console.MarkupLineInterpolated($"  Message IDs: {result.FirstMessageId} → {result.LastMessageId}");
                _console.MarkupLineInterpolated($"  Size:        {result.AverageMessageSize} avg. ({result.TotalSize} total)");
                _console.MarkupLineInterpolated($"  Time:        {result.FirstTimestamp} UTC → {result.LastTimestamp} UTC");
            }
            else
            {
                _console.MarkupLineInterpolated($"  Message ID:  {result.FirstMessageId}");
                _console.MarkupLineInterpolated($"  Size:        {result.TotalSize}");
                _console.MarkupLineInterpolated($"  Timestamp:   {result.FirstTimestamp} UTC");
            } 
        }
    }
}