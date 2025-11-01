using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RmqCli.Core.Models;
using RmqCli.Shared.Factories;

namespace RmqCli.Shared;

public class QueueValidator
{
    private readonly ILogger<QueueValidator> _logger;

    public QueueValidator(ILogger<QueueValidator> logger)
    {
        _logger = logger;
    }

    public async Task<QueueInfo> ValidateAsync(
        IChannel channel,
        string queue,
        CancellationToken token)
    {
        try
        {
            var queueDeclareOk = await channel.QueueDeclarePassiveAsync(queue, token);
            _logger.LogDebug("Queue '{Queue}' exists with {MessageCount} messages and {ConsumerCount} consumers",
                queueDeclareOk.QueueName, queueDeclareOk.MessageCount, queueDeclareOk.ConsumerCount);
            return QueueInfo.Create(queueDeclareOk);
        }
        catch (OperationInterruptedException ex)
        {
            _logger.LogError(ex, "Queue '{Queue}' not found. Reply code: {ReplyCode}, Reply text: {ReplyText}",
                queue, ex.ShutdownReason?.ReplyCode, ex.ShutdownReason?.ReplyText);

            var queueNotFoundError = RabbitErrorInfoFactory.QueueNotFound(queue);
            return QueueInfo.CreateError(queue, queueNotFoundError);
        }
    }
}