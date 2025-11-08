using System.Text.Json;
using RmqCli.Shared;
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
        _console = AnsiConsoleFactory.CreateStderrConsole();
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
        _console.MarkupLineInterpolated($"  [dim]Queue:      {response.Queue}[/]");

        if (response.Result is { } result)
        {
            _console.MarkupLineInterpolated($"  [dim]Mode:       {result.RetrievalMode}[/]");
            _console.MarkupLineInterpolated($"  [dim]Ack Mode:   {result.AckMode}[/]");
            _console.MarkupLineInterpolated($"  [dim]Received:   {FormatMessageCount(result.MessagesReceived)}[/]");

            // Show processed count with skipped messages if applicable
            if (result.MessagesSkipped > 0)
            {
                _console.MarkupLineInterpolated(
                    $"  [dim]Processed:  {FormatMessageCount(result.MessagesProcessed)} ({result.MessagesSkipped} skipped & requeued by RabbitMQ)[/]");
            }
            else
            {
                _console.MarkupLineInterpolated($"  [dim]Processed:  {FormatMessageCount(result.MessagesProcessed)}[/]");
            }

            _console.MarkupLineInterpolated($"  [dim]Total size: {result.TotalSize}[/]");
        }
    }

    private void WriteMessageRetrievalResultInJsonFormat(MessageRetrievalResponse response)
    {
        var resultJson = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.MessageRetrievalResponse);
        Console.Error.WriteLine(resultJson);
    }

    private static string FormatMessageCount(long count)
    {
        var pluralSuffix = count == 1 ? string.Empty : "s";
        return $"{count} message{pluralSuffix}";
    }
}