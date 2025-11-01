using RabbitMQ.Client;

namespace RmqCli.Core.Models;

public record RabbitMessage(
    string Exchange,
    string RoutingKey,
    string Queue,
    string Body,
    ulong DeliveryTag,
    IReadOnlyBasicProperties? Props,
    bool Redelivered
);
