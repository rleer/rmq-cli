using RmqCli.Core.Models;

namespace RmqCli.Infrastructure.RabbitMq;

public static class ConsumeErrorInfoFactory
{
    public static ErrorInfo QueueNotFoundErrorInfo(string queueName)
    {
        return new ErrorInfo
        {
            Category = "routing",
            Code = "QUEUE_NOT_FOUND",
            Error = $"Queue '{queueName}' not found",
            Suggestion = "Check if the queue exists and is correctly configured"
        };
    }
}
