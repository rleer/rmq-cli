using RmqCli.Core.Models;

namespace RmqCli.Commands.Purge;

public class PurgeResponse : Response
{
   public string Queue { get; set; } = string.Empty;
   public string Vhost { get; set; } = string.Empty;
   public string Operation { get; set; } = "purge";
   public uint PurgedMessages { get; set; }
}