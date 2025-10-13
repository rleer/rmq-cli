namespace RmqCli.Shared;

public static class CancellationHelper
{
   public static CancellationTokenSource LinkWithCtrlCHandler(CancellationToken ct) 
   {
       var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
       Console.CancelKeyPress += (_, eventArgs) =>
       {
           eventArgs.Cancel = true; // Prevent the process from terminating.
           cts.Cancel();
       };
       return cts;
   }
}