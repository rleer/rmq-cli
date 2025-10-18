using System.Text.Json;
using RmqCli.Infrastructure.Output;
using RmqCli.Shared;
using RmqCli.Shared.Json;
using Spectre.Console;
using AnsiConsoleFactory = RmqCli.Shared.Factories.AnsiConsoleFactory;

namespace RmqCli.Commands.Consume;

public interface IConsumeOutputService
{
    void WriteConsumeResult(ConsumeResponse response);
}

public class ConsumeOutputService : IConsumeOutputService
{
    private readonly OutputOptions _outputOptions;
    private readonly IAnsiConsole _console;

    public ConsumeOutputService(OutputOptions outputOptions)
    {
        _outputOptions = outputOptions;
        _console = AnsiConsoleFactory.CreateStderrConsole();
    }

    public void WriteConsumeResult(ConsumeResponse response)
    {
        if (_outputOptions.Quiet)
            return;

        if (_outputOptions.Format == OutputFormat.Json)
        {
            WriteConsumeResultInJsonFormat(response);
        }
        else
        {
            WriteConsumeResultInPlainFormat(response);
        }
    }

    private void WriteConsumeResultInJsonFormat(ConsumeResponse response)
    {
        var resultJson = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscapingOptions.GetTypeInfo(typeof(ConsumeResponse)));
        Console.Error.WriteLine(resultJson);
    }

    private void WriteConsumeResultInPlainFormat(ConsumeResponse response)
    {
        if (response.Queue is not null)
        {
            _console.MarkupLineInterpolated($"  [dim]Queue:      {response.Queue}[/]");
        }

        if (response.Result is { } result)
        {
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