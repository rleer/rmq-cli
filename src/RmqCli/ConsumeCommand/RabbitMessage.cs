using RabbitMQ.Client;

namespace RmqCli.ConsumeCommand;

public record RabbitMessage(
    string Body,
    ulong DeliveryTag,
    IReadOnlyBasicProperties? Props,
    bool Redelivered
);
