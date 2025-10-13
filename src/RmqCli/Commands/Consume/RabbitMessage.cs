using RabbitMQ.Client;

namespace RmqCli.Commands.Consume;

public record RabbitMessage(
    string Body,
    ulong DeliveryTag,
    IReadOnlyBasicProperties? Props,
    bool Redelivered
);
