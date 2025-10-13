using RmqCli.Core.Models;

namespace RmqCli.Infrastructure.RabbitMq;

public static class RabbitErrorInfoFactory
{
    public static ErrorInfo OperationInterrupted(string reason, string code)
    {
        return new ErrorInfo
        {
            Category = "connection",
            Code = "RABBITMQ_OPERATION_INTERRUPTED",
            Error = $"{reason} ({code})",
            Suggestion = "Check RabbitMQ server status and network connectivity"
        };
    }
    public static ErrorInfo VirtualHostNotFound(string vhost)
    {
        return new ErrorInfo
        {
            Category = "connection",
            Code = "VIRTUAL_HOST_NOT_FOUND",
            Error = $"Virtual host '{vhost}' not found",
            Suggestion = "Check if the virtual host exists and is correctly configured"
        };
    }
    public static ErrorInfo AccessDenied(string user, string vhost)
    {
        return new ErrorInfo
        {
            Category = "connection",
            Code = "ACCESS_DENIED",
            Error = $"Access denied for user '{user}' to virtual host '{vhost}'",
            Suggestion = "Check user permissions and virtual host configuration"
        };
    }
    public static ErrorInfo AuthenticationFailed(string user)
    {
        return new ErrorInfo
        {
            Category = "connection",
            Code = "AUTHENTICATION_FAILED",
            Error = $"Authentication failed for user '{user}'",
            Suggestion = "Check username and password"
        };
    }
    public static ErrorInfo ConnectionFailed(string host, int port)
    {
        return new ErrorInfo
        {
            Category = "connection",
            Code = "CONNECTION_FAILED",
            Error = $"Could not connect to RabbitMQ at {host}:{port}",
            Suggestion = "Check RabbitMQ server status and network connectivity"
        };
    }
    public static ErrorInfo BrokerUnreachable(string host, int port)
    {
        return new ErrorInfo
        {
            Category = "connection",
            Code = "BROKER_UNREACHABLE",
            Error = $"RabbitMQ broker unreachable at {host}:{port}",
            Suggestion = "Check RabbitMQ server status and network connectivity"
        };
    }
}