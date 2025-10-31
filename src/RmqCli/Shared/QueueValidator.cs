using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Output;
using RmqCli.Infrastructure.RabbitMq;

namespace RmqCli.Shared;

public class QueueValidator
{
    private readonly ILogger<QueueValidator> _logger;
    private readonly IStatusOutputService _statusOutput;

    public QueueValidator(ILogger<QueueValidator> logger, IStatusOutputService statusOutput)
    {
        _logger = logger;
        _statusOutput = statusOutput;
    }

    public async Task<QueueInfo?> ValidateAsync(
        IChannel channel,
        string queue,
        string operationName, // e.g., "peek", "consume"
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

            var queueNotFoundError = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queue);
            _statusOutput.ShowError($"Failed to {operationName} from queue", queueNotFoundError);

            return null;
        }
    }
}