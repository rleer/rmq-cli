namespace RmqCli.Commands.Purge;

public class PurgeOptions
{
    public required string Queue { get; init;  }
    public bool Force { get; init;  }
}