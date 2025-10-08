using RmqCli.Common;

namespace RmqCli.ConsumeCommand.MessageFormatter;

public interface IMessageFormatterFactory
{
    IMessageFormatter CreateFormatter(OutputFormat format);
}

public class MessageFormatterFactory : IMessageFormatterFactory
{
    private readonly IEnumerable<IMessageFormatter> _formatters;

    public MessageFormatterFactory(IEnumerable<IMessageFormatter> formatters)
    {
        _formatters = formatters;
    }

    public IMessageFormatter CreateFormatter(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Plain => _formatters.First(formatter => formatter is TextMessageFormatter),
            OutputFormat.Table => throw new NotImplementedException("Table formatter is not yet implemented"),
            OutputFormat.Json => _formatters.First(formatter => formatter is JsonMessageFormatter),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown output format")
        };
    }
}