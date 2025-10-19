using RabbitMQ.Client;

namespace RmqCli.Commands.Consume;

public class QueueInfo
{
    public bool Exists { get; init; }
    public string Queue { get; init; } = string.Empty;
    public int MessageCount { get; init; }
    public int ConsumerCount { get; init; }

    public static QueueInfo Create(QueueDeclareOk queueDeclareOk)
    {
        return new QueueInfo
        {
            Exists = true,
            Queue = queueDeclareOk.QueueName,
            MessageCount = (int)queueDeclareOk.MessageCount,
            ConsumerCount = (int)queueDeclareOk.ConsumerCount
        };
    }
}