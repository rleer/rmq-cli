using RmqCli;
using RmqCli.Commandhandler;
using Spectre.Console;

try
{
    var serviceFactory = new ServiceFactory();
    var rootCommandHandler = new RootCommandHandler(serviceFactory);
    return await rootCommandHandler.RunAsync(args);
}
catch (Exception e)
{
    AnsiConsole.MarkupLineInterpolated($"[indianred1]⚠ An error occurred: {e.Message}[/]");
    return 1;
}