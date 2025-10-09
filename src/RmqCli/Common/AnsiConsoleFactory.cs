using Spectre.Console;

namespace RmqCli.Common;

public static class AnsiConsoleFactory
{
    public static IAnsiConsole CreateStderrConsole()
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
            Interactive = InteractionSupport.Yes // hardcoded to allow interactive features (e.g. progress bars) when redirecting stdout or piping input
        });
    }

    public static IAnsiConsole CreateStdoutConsole()
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Out),
            Interactive = InteractionSupport.Yes // hardcoded to allow interactive features (e.g. progress bars) when redirecting stdout or piping input
        });
    }
}