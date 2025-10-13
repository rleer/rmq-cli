using RmqCli;
using RmqCli.Commands;
using Spectre.Console;

try
{
    var serviceFactory = new ServiceFactory();
    var rootCommandHandler = new RootCommandHandler(serviceFactory);
    return await rootCommandHandler.RunAsync(args);
}
catch (Exception e)
{
    AnsiConsole.MarkupLineInterpolated($"[red3]⚠ An error occurred: {e.Message}[/]");
    return 1;
}