using RabbitMQ.Client;

namespace RmqCli.Core.Models;

public class QueueInfo
{
    public bool Exists { get; init; }
    public string Queue { get; init; } = string.Empty;
    public int MessageCount { get; init; }
    public int ConsumerCount { get; init; }
    public ErrorInfo? QueueError { get; init; }
    public bool HasError { get; set; }

    public static QueueInfo Create(QueueDeclareOk queueDeclareOk)
    {
        return new QueueInfo
        {
            Exists = true,
            Queue = queueDeclareOk.QueueName,
            MessageCount = (int)queueDeclareOk.MessageCount,
            ConsumerCount = (int)queueDeclareOk.ConsumerCount,
            HasError = false
        };
    }

    public static QueueInfo CreateError(string queue, ErrorInfo errorInfo)
    {
        return new QueueInfo
        {
            Exists = false,
            Queue = queue,
            MessageCount = 0,
            ConsumerCount = 0,
            QueueError = errorInfo,
            HasError = true
        };
    }
}