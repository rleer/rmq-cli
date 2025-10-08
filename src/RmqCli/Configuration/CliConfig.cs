using RmqCli.Common;

namespace RmqCli.Configuration;

public class CliConfig
{
    public OutputFormat Format { get; set; }
    public bool Quiet { get; set; }
    public bool Verbose { get; set; }
    public bool NoColor { get; set; }
}