namespace RmqCli.Infrastructure.RabbitMq;

public interface IRabbitManagementClient : IDisposable
{
    /// <summary>
    /// Purges all ready messages from the specified queue.
    /// </summary>
    Task<ManagementApiResponse> PurgeQueueAsync(
        string queueName,
        CancellationToken cancellationToken = default);
}