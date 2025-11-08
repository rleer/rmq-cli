using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Shared.Factories;
using RmqCli.Shared.Output;

namespace RmqCli.Infrastructure.RabbitMq;

public interface IRabbitChannelFactory
{
    Task<IChannel> GetChannelAsync();
    Task<IChannel> GetChannelWithPublisherConfirmsAsync();
    Task CloseConnectionAsync();
}

public class RabbitChannelFactory : IRabbitChannelFactory
{
    private readonly RabbitMqConfig _config;
    private readonly ILogger<RabbitChannelFactory> _logger;
    private readonly ConnectionFactory _connectionFactory;
    private readonly IStatusOutputService _output;

    private IConnection? _connection;

    public RabbitChannelFactory(RabbitMqConfig rabbitMqConfig, ILogger<RabbitChannelFactory> logger, IStatusOutputService output)
    {
        _config = rabbitMqConfig;
        _logger = logger;
        _output = output;
        _connectionFactory = new ConnectionFactory
        {
            HostName = _config.Host,
            Port = _config.Port,
            UserName = _config.User,
            Password = _config.Password,
            VirtualHost = _config.VirtualHost,
            ClientProvidedName = _config.ClientName
        };
    }

    public async Task<IChannel> GetChannelAsync()
    {
        try
        {
            var connection = await GetConnectionAsync();
            return await GetChannelAsync(connection);
        }
        catch (Exception ex)
        {
            HandleConnectionException(ex);
            throw;
        }
    }

    public async Task<IChannel> GetChannelWithPublisherConfirmsAsync()
    {
        try
        {
            var connection = await GetConnectionAsync();

            _logger.LogDebug("Acquiring RabbitMQ channel with publisher confirmations enabled");
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(256)); // TODO: Should be configurable?
            var channel = await connection.CreateChannelAsync(channelOptions);

            // Register event handlers for channel events
            channel.BasicReturnAsync += OnChannelOnBasicReturnAsync;
            channel.ChannelShutdownAsync += OnChannelOnChannelShutdownAsync;

            return channel;
        }
        catch (Exception ex)
        {
            HandleConnectionException(ex);
            throw;
        }
    }

    private async Task<IChannel> GetChannelAsync(IConnection connection)
    {
        _logger.LogDebug("Acquiring RabbitMQ channel");
        var channel = await connection.CreateChannelAsync();

        // Register event handlers for channel events
        channel.BasicReturnAsync += OnChannelOnBasicReturnAsync;
        channel.ChannelShutdownAsync += OnChannelOnChannelShutdownAsync;

        return channel;
    }

    private async Task<IConnection> GetConnectionAsync()
    {
        if (_connection is { IsOpen: true })
        {
            _logger.LogDebug("Reusing existing RabbitMQ connection");
            return _connection;
        }

        _logger.LogDebug("Connecting to RabbitMQ, host={Host}, port={Port}, vhost={VirtualHost}, client={ClientName}",
            _config.Host, _config.Port, _config.VirtualHost, _config.ClientName);
        _connection = await _connectionFactory.CreateConnectionAsync();

        _connection.ConnectionShutdownAsync += (_, args) =>
        {
            if (args.ReplyCode == 200)
            {
                _logger.LogInformation("RabbitMQ connection shut down: {Reason} ({ReasonCode})", args.ReplyText, args.ReplyCode);
                return Task.CompletedTask;
            }

            _logger.LogWarning("RabbitMQ connection shut down due to a failure: {Reason} ({ReasonCode})", args.ReplyText, args.ReplyCode);
            return Task.CompletedTask;
        };

        return _connection;
    }

    private void HandleConnectionException(Exception ex)
    {
        // Check the entire exception chain to find the most specific error
        var specificException = GetMostSpecificException(ex);

        switch (specificException)
        {
            case OperationInterruptedException opEx:
                var errorCode = opEx.ShutdownReason?.ReplyCode ?? 0;
                var errorText = opEx.ShutdownReason?.ReplyText ?? "Unknown reason";

                _logger.LogError(opEx, "{Reason} (code:{Code})", errorText, errorCode == 0 ? "unknown" : errorCode);

                if (errorCode == 530)
                {
                    if (errorText.Contains("not found"))
                    {
                        var virtualHostNotFound = RabbitErrorInfoFactory.VirtualHostNotFound(_config.VirtualHost);
                        _output.ShowError("Connection failed", virtualHostNotFound);
                    }
                    else if (errorText.Contains("refused") || errorText.Contains("access") && errorText.Contains("denied"))
                    {
                        var accessDeniedError = RabbitErrorInfoFactory.AccessDenied(_config.User, _config.VirtualHost);
                        _output.ShowError("Connection failed", accessDeniedError);
                    }
                }

                var operationInterruptedError = RabbitErrorInfoFactory.OperationInterrupted(
                    errorText,
                    errorCode.ToString());
                _output.ShowError("RabbitMQ operation interrupted", operationInterruptedError);
                break;
            case AuthenticationFailureException authEx:
                _logger.LogError(authEx, "Authentication failed for user '{User}'", _config.User);

                var authenticationError = RabbitErrorInfoFactory.AuthenticationFailed(_config.User);
                _output.ShowError("Connection failed", authenticationError);
                break;
            case ConnectFailureException connectEx:
                _logger.LogError(connectEx, "Could not connect to {Host}:{Port}", _config.Host, _config.Port);

                var connectionError = RabbitErrorInfoFactory.ConnectionFailed(_config.Host, _config.Port);
                _output.ShowError("Connection failed", connectionError);
                break;
            case BrokerUnreachableException brokerEx:
                _logger.LogError(brokerEx, "RabbitMQ connection failed: Broker unreachable at {Host}:{Port}", _config.Host, _config.Port);

                var brokerUnreachableError = RabbitErrorInfoFactory.BrokerUnreachable(_config.Host, _config.Port);
                _output.ShowError("Connection failed", brokerUnreachableError);
                break;
            default:
                var genericError = ErrorInfoFactory.GenericErrorInfo(
                    "Unexpected connection error",
                    "RABBITMQ_CONNECTION_ERROR",
                    "Check RabbitMQ server status and configuration",
                    "connection",
                    specificException);
                _logger.LogError(specificException, "Unexpected RabbitMQ connection error");
                _output.ShowError("Connection failed", genericError);
                break;
        }
    }

    /// <summary>
    /// Traverses the exception chain to find the most specific RabbitMQ exception.
    /// This handles cases where specific exceptions are wrapped inside generic ones.
    /// </summary>
    private static Exception GetMostSpecificException(Exception ex)
    {
        var current = ex;
        Exception? mostSpecific = null;

        // Define exception priority order (most specific first)
        var priorityOrder = new[]
        {
            typeof(AuthenticationFailureException),
            typeof(OperationInterruptedException),
            typeof(ConnectFailureException),
            typeof(BrokerUnreachableException)
        };

        // Traverse the exception chain
        while (current != null)
        {
            // Check if this exception type has higher priority than what we've found
            var currentPriority = Array.IndexOf(priorityOrder, current.GetType());
            if (currentPriority >= 0)
            {
                var existingPriority = mostSpecific != null ? Array.IndexOf(priorityOrder, mostSpecific.GetType()) : int.MaxValue;
                if (currentPriority < existingPriority)
                {
                    mostSpecific = current;
                }
            }

            current = current.InnerException;
        }

        // Return the most specific exception found, or the original if none matched our priority list
        return mostSpecific ?? ex;
    }

    public async Task CloseConnectionAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }

    private Task OnChannelOnChannelShutdownAsync(object _, ShutdownEventArgs @event)
    {
        _logger.LogInformation("RabbitMQ channel shut down: {ReplyText} ({ReplyCode})", @event.ReplyText, @event.ReplyCode);
        return Task.CompletedTask;
    }

    private Task OnChannelOnBasicReturnAsync(object _, BasicReturnEventArgs @event)
    {
        _logger.LogWarning("Message returned: {ReplyText} ({ReplyCode})", @event.ReplyText, @event.ReplyCode);
        return Task.CompletedTask;
    }
}