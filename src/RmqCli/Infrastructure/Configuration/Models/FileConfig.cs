namespace RmqCli.Infrastructure.Configuration.Models;

public class FileConfig
{
    public string MessageDelimiter { get; set; } = Environment.NewLine;
    public int MessagesPerFile { get; set; }
}