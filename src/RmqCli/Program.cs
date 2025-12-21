using RmqCli.Commands;
using RmqCli.Shared.Factories;
using Spectre.Console;

try
{
    var serviceFactory = new ServiceFactory();
    var rootCommandHandler = new RootCommandHandler(serviceFactory);
    return await rootCommandHandler.RunAsync(args);
}
catch (Exception e)
{
    if (args.Contains("--no-color"))
    {
        Console.Error.WriteLine($"⚠ An error occurred: {e.Message}");
        return 1;
    }

    AnsiConsole.MarkupLineInterpolated($"[indianred_1]⚠ An error occurred: {e.Message}[/]");
    return 1;
}