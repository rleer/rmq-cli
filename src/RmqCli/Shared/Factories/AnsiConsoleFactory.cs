using Spectre.Console;

namespace RmqCli.Shared.Factories;

public static class AnsiConsoleFactory
{
    public static IAnsiConsole CreateStderrConsole(bool noColor = false)
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = noColor ? AnsiSupport.No : AnsiSupport.Yes,
            ColorSystem = noColor ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
            Interactive = InteractionSupport.Yes // hardcoded to allow interactive features (e.g. progress bars) when redirecting stdout or piping input
        });
    }
}