using System.Text.Json;
using RmqCli.Infrastructure.Output;
using RmqCli.Shared;
using RmqCli.Shared.Json;
using Spectre.Console;
using AnsiConsoleFactory = RmqCli.Shared.Factories.AnsiConsoleFactory;

namespace RmqCli.Commands.Peek;

public interface IPeekOutputService
{
    void WritePeekResult(PeekResponse response);
}

public class PeekOutputService : IPeekOutputService
{
    private readonly OutputOptions _outputOptions;
    private readonly IAnsiConsole _console;

    public PeekOutputService(OutputOptions outputOptions)
    {
        _outputOptions = outputOptions;
        _console = AnsiConsoleFactory.CreateStderrConsole();
    }

    public void WritePeekResult(PeekResponse response)
    {
        if (_outputOptions.Quiet)
            return;

        if (_outputOptions.Format == OutputFormat.Json)
        {
            WritePeekResultInJsonFormat(response);
        }
        else
        {
            WritePeekResultInPlainFormat(response);
        }
    }

    private void WritePeekResultInJsonFormat(PeekResponse response)
    {
        var resultJson = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(PeekResponse)));
        Console.Error.WriteLine(resultJson);
    }

    private void WritePeekResultInPlainFormat(PeekResponse response)
    {
        if (response.Queue is not null)
        {
            _console.MarkupLineInterpolated($"  [dim]Queue:      {response.Queue}[/]");
        }

        if (response.Result is { } result)
        {
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
            _console.MarkupLineInterpolated($"  [dim]Output:     {result.OutputDestination} ({result.OutputFormat} format)[/]");
            _console.MarkupLineInterpolated($"  [dim]Total size: {result.TotalSize}[/]");
        }
    }

    private static string FormatMessageCount(long count)
    {
        var pluralSuffix = count == 1 ? string.Empty : "s";
        return $"{count} message{pluralSuffix}";
    }
}
