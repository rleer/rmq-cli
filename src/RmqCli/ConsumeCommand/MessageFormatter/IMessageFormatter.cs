namespace RmqCli.ConsumeCommand.MessageFormatter;

public interface IMessageFormatter
{
    string FormatMessage(RabbitMessage message);
    string FormatMessages(IEnumerable<RabbitMessage> messages);
}
