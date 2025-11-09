using RmqCli.Core.Models;

namespace RmqCli.Shared.Factories;

public static class RabbitErrorInfoFactory
{
    public static ErrorInfo QueueNotFound(string queueName)
    {
        return new ErrorInfo
        {
            Error = $"Queue '{queueName}' not found",
            Suggestion = "Check if the queue exists and is correctly configured"
        };
    }

    public static ErrorInfo OperationInterrupted(string reason, string code)
    {
        return new ErrorInfo
        {
            Error = $"{reason} ({code})",
            Suggestion = "Check RabbitMQ server status and network connectivity"
        };
    }

    public static ErrorInfo VirtualHostNotFound(string vhost)
    {
        return new ErrorInfo
        {
            Error = $"Virtual host '{vhost}' not found",
            Suggestion = "Check if the virtual host exists and is correctly configured"
        };
    }

    public static ErrorInfo AccessDenied(string user, string vhost)
    {
        return new ErrorInfo
        {
            Error = $"Access denied for user '{user}' to virtual host '{vhost}'",
            Suggestion = "Check user permissions and virtual host configuration"
        };
    }

    public static ErrorInfo AuthenticationFailed(string user)
    {
        return new ErrorInfo
        {
            Error = $"Authentication failed for user '{user}'",
            Suggestion = "Check username and password"
        };
    }

    public static ErrorInfo ConnectionFailed(string host, int port)
    {
        return new ErrorInfo
        {
            Error = $"Could not connect to RabbitMQ at {host}:{port}",
            Suggestion = "Check RabbitMQ server status and network connectivity"
        };
    }

    public static ErrorInfo BrokerUnreachable(string host, int port)
    {
        return new ErrorInfo
        {
            Error = $"RabbitMQ broker unreachable at {host}:{port}",
            Suggestion = "Check RabbitMQ server status and network connectivity"
        };
    }
}