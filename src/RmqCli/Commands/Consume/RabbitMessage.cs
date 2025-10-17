using RabbitMQ.Client;

namespace RmqCli.Commands.Consume;

public record RabbitMessage(
    string Exchange,
    string RoutingKey,
    string Body,
    ulong DeliveryTag,
    IReadOnlyBasicProperties? Props,
    bool Redelivered
);
