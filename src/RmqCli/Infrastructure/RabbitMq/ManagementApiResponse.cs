using RmqCli.Core.Models;

namespace RmqCli.Infrastructure.RabbitMq;

/// <summary>
/// Response wrapper for RabbitMQ Management API calls
/// </summary>
public class ManagementApiResponse
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public ErrorInfo? ErrorInfo { get; set; }
}

/// <summary>
/// Generic response wrapper for RabbitMQ Management API calls with data
/// </summary>
public class ManagementApiResponse<T> : ManagementApiResponse
{
    public T? Data { get; init; }
}

public class ManagementApiErrorResponse
{
    public string Error { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}