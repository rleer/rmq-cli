using System.Text.Json;
using RmqCli.Infrastructure.Configuration.Models;
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
    private readonly CliConfig _cliConfig;
    private readonly IAnsiConsole _console;

    public ConsumeOutputService(CliConfig cliConfig)
    {
        _cliConfig = cliConfig;
        _console = AnsiConsoleFactory.CreateStderrConsole();
    }

    public void WriteConsumeResult(ConsumeResponse response)
    {
        if (_cliConfig.Quiet)
            return;

        if (_cliConfig.Format == OutputFormat.Json)
            WriteConsumeResultInJsonFormat(response);

        if (_cliConfig.Format == OutputFormat.Plain)
            WriteConsumeResultInPlainFormat(response);
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
            _console.MarkupLineInterpolated($"  Queue:      {response.Queue}");
        }

        if (response.Result is { } result)
        {
            _console.MarkupLineInterpolated($"  Ack Mode:   {result.AckMode}");
            _console.MarkupLineInterpolated($"  Received:   {FormatMessageCount(result.MessagesReceived)}");

            // Show processed count with skipped messages if applicable
            if (result.MessagesSkipped > 0)
            {
                _console.MarkupLineInterpolated(
                    $"  Processed:  {FormatMessageCount(result.MessagesProcessed)} ({result.MessagesSkipped} skipped, requeued by RabbitMQ)");
            }
            else
            {
                _console.MarkupLineInterpolated($"  Processed:  {FormatMessageCount(result.MessagesProcessed)}");
            }

            _console.MarkupLineInterpolated($"  Output:     {result.OutputDestination} ({result.OutputFormat} format)");
            _console.MarkupLineInterpolated($"  Total size: {result.TotalSize}");
        }
    }

    private static string FormatMessageCount(long count)
    {
        var pluralSuffix = count == 1 ? string.Empty : "s";
        return $"{count} message{pluralSuffix}";
    }
}