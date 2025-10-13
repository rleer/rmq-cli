using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output.Formatters.Json;
using RmqCli.Shared;
using Spectre.Console;
using AnsiConsoleFactory = RmqCli.Shared.Factories.AnsiConsoleFactory;

namespace RmqCli.Infrastructure.Output;

public interface IStatusOutputService
{
    void ShowStatus(string message);
    void ShowSuccess(string message);
    void ShowWarning(string message, bool addNewLine = false);
    void ShowError(string message, ErrorInfo? errorInfo = null);
    Task ExecuteWithProgress(string description, int maxValue, Func<IProgress<int>, Task> workload);
    bool NoColor { get; }
}

// ILogger -> stderr (technical diagnostics, enable with --verbose)
// AnsiConsole -> stderr (user-friendly output)
// (Json)Console -> stdout for results(structured output for automation)
public class StatusOutputService : IStatusOutputService
{
    private const string SuccessSymbol = "\u2714"; // ✓
    private const string WarningSymbol = "\u26A0"; // ⚠
    private const string ErrorSymbol = "\u2717"; // ✗
    private const string StatusSymbol = "\u26EF"; // ⛯

    private readonly CliConfig _cliConfig;
    private readonly IAnsiConsole _console;

    public StatusOutputService(CliConfig cliConfig)
    {
        _cliConfig = cliConfig;
        _console = AnsiConsoleFactory.CreateStderrConsole();
    }

    public bool NoColor => _cliConfig.NoColor;

    /// <summary>
    /// Prints a status message to STDERR console.
    /// </summary>
    /// <param name="message"></param>
    public void ShowStatus(string message)
    {
        if (_cliConfig.Quiet)
            return;

        _console.MarkupLine($"{StatusSymbol} {message}");
    }

    /// <summary>
    /// Prints a success message to STDERR console.
    /// </summary>
    /// <param name="message"></param>
    public void ShowSuccess(string message)
    {
        if (_cliConfig.Quiet)
            return;

        _console.MarkupLine($"{SuccessSymbol} {message}");
    }

    /// <summary>
    /// Prints a warning message to STDERR console.
    /// If `addNewLine` is true, it adds an extra new line before the message. Useful for operations that are cancelled or interrupted,
    /// </summary>
    /// <param name="message"></param>
    /// <param name="addNewLine"></param>
    public void ShowWarning(string message, bool addNewLine = false)
    {
        if (_cliConfig.Quiet)
            return;

        if (addNewLine)
            _console.WriteLine();

        _console.MarkupLine($"{WarningSymbol} {message}");
    }

    /// <summary>
    /// Prints an error message to STDERR console.
    /// If `errorInfo` is provided, it will print additional details about the error.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="errorInfo"></param>
    public void ShowError(string message, ErrorInfo? errorInfo = null)
    {
        switch (_cliConfig.Format)
        {
            case OutputFormat.Plain:
            {
                _console.MarkupLine($"{ErrorSymbol} {message}");
                if (errorInfo is null)
                    return;

                _console.MarkupLine($"  Error: {EscapeMarkup(errorInfo.Error)}");
                _console.MarkupLine($"  Category: {EscapeMarkup(errorInfo.Category)}");
                if (!string.IsNullOrWhiteSpace(errorInfo.Suggestion))
                {
                    _console.MarkupLine($"  Suggestion: {EscapeMarkup(errorInfo.Suggestion)}");
                }

                break;
            }
            case OutputFormat.Table:
            case OutputFormat.Json when errorInfo is null:
                return;
            case OutputFormat.Json:
            {
                var ctx = new JsonSerializationContext(new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = false,
                    TypeInfoResolver = JsonSerializationContext.Default,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var response = new Response
                {
                    Status = "error",
                    Timestamp = DateTime.Now,
                    Error = errorInfo
                };

                var serializedError = JsonSerializer.Serialize(response, ctx.Response);
                System.Console.Error.WriteLine(serializedError);
                break;
            }
        }
    }

    public async Task ExecuteWithProgress(string description, int maxValue, Func<IProgress<int>, Task> workload)
    {
        // TODO: Make progress bar threshold configurable
        if (maxValue < 3000 || _cliConfig.Quiet)
        {
            // For quiet mode or JSON output and low number of messages to publish, provide a no-op progress reporter
            await workload(new Progress<int>());
        }
        else
        {
            await AnsiConsole.Progress()
                .AutoClear(true)
                .HideCompleted(true)
                .Columns(
                    new SpinnerColumn(Spinner.Known.Dots),
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn())
                .StartAsync(async ctx =>
                {
                    var progressTask = ctx.AddTask(description, maxValue: maxValue);

                    var progress = new Progress<int>(value => { progressTask.Value = value; });

                    await workload(progress);
                });
        }
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
}