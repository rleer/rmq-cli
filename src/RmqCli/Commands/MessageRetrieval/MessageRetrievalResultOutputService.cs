using System.Text.Json;
using RmqCli.Shared.Json;
using RmqCli.Shared.Output;
using Spectre.Console;
using AnsiConsoleFactory = RmqCli.Shared.Factories.AnsiConsoleFactory;

namespace RmqCli.Commands.MessageRetrieval;

public class MessageRetrievalResultOutputService
{
    private readonly OutputOptions _outputOptions;
    private readonly IAnsiConsole _console;

    public MessageRetrievalResultOutputService(OutputOptions outputOptions)
    {
        _outputOptions = outputOptions;
        _console = AnsiConsoleFactory.CreateStderrConsole(_outputOptions.NoColor);
    }

    public void WriteMessageRetrievalResult(MessageRetrievalResponse response)
    {
        if (_outputOptions.Quiet)
            return;

        if (_outputOptions.Format == OutputFormat.Json)
        {
            WriteMessageRetrievalResultInJsonFormat(response);
        }
        else
        {
            WriteMessageRetrievalResultInPlainFormat(response);
        }
    }

    private void WriteMessageRetrievalResultInPlainFormat(MessageRetrievalResponse response)
    {
        var style = _outputOptions.NoColor ? Style.Plain : Style.Parse("dim");
        var rows = new List<Text> { new($"  Queue:      {response.Queue}", style) };

        if (response.Result is { } result)
        {
            rows.Add(new Text($"  Mode:       {result.RetrievalMode}", style));
            rows.Add(new Text($"  Ack Mode:   {result.AckMode}", style));
            rows.Add(new Text($"  Received:   {FormatMessageCount(result.MessagesReceived)}", style));

            // Show processed count with skipped messages if applicable
            if (result.MessagesSkipped > 0)
            {
                rows.Add(new Text($"  Processed:  {FormatMessageCount(result.MessagesProcessed)} ({result.MessagesSkipped} skipped & requeued by RabbitMQ)",
                    style));
            }
            else
            {
                rows.Add(new Text($"  Processed:  {FormatMessageCount(result.MessagesProcessed)}", style));
            }

            rows.Add(new Text($"  Total size: {result.TotalSize}", style));
        }

        _console.Write(new Rows(rows));
        return;
        
        string FormatMessageCount(long count)
        {
            var pluralSuffix = count == 1 ? string.Empty : "s";
            return $"{count} message{pluralSuffix}";
        }
    }

    private void WriteMessageRetrievalResultInJsonFormat(MessageRetrievalResponse response)
    {
        var resultJson = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.MessageRetrievalResponse);
        Console.Error.WriteLine(resultJson);
    }
}