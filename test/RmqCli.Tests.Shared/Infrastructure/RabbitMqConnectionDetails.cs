namespace RmqCli.Tests.Shared.Infrastructure;

/// <summary>
/// RabbitMQ connection details for CLI commands
/// </summary>
public record RabbitMqConnectionDetails(
    string Host,
    int AmqpPort,
    int ManagementPort,
    string Username,
    string Password);