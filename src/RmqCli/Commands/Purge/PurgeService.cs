using Microsoft.Extensions.Logging;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.RabbitMq;
using RmqCli.Shared.Output;
using Spectre.Console;

namespace RmqCli.Commands.Purge;

public interface IPurgeService
{
    Task<int> PurgeQueueAsync(CancellationToken cancellationToken = default);
}

public class PurgeService : IPurgeService
{
    private readonly IRabbitManagementClient _client;
    private readonly PurgeOptions _purgeOptions;
    private readonly RabbitMqConfig _rabbitMqConfig;
    private readonly IPurgeOutputService _resultOutput;
    private readonly IStatusOutputService _statusOutput;
    private readonly ILogger<PurgeService> _logger;

    public PurgeService(
        IRabbitManagementClient client,
        PurgeOptions purgeOptions,
        RabbitMqConfig rabbitMqConfig,
        IPurgeOutputService resultOutput,
        IStatusOutputService statusOutput,
        ILogger<PurgeService> logger)
    {
        _client = client;
        _purgeOptions = purgeOptions;
        _rabbitMqConfig = rabbitMqConfig;
        _resultOutput = resultOutput;
        _logger = logger;
        _statusOutput = statusOutput;
    }

    public async Task<int> PurgeQueueAsync(CancellationToken cancellationToken = default)
    {
        var formattedQueueName = _statusOutput.NoColor ? _purgeOptions.Queue : $"[orange1]{_purgeOptions.Queue}[/]";
        var formattedVhostName = _statusOutput.NoColor ? _rabbitMqConfig.VirtualHost : $"[orange1]{_rabbitMqConfig.VirtualHost}[/]";
        _statusOutput.ShowStatus($"Purging queue {formattedQueueName} in vhost {formattedVhostName}");

        if (!_purgeOptions.Force)
        {
            var confirmation = AnsiConsole.Prompt(new ConfirmationPrompt("Are you sure you want to purge this queue?").HideDefaultValue());
            if (!confirmation)
            {
                _statusOutput.ShowError("Purge operation cancelled by user.");
                return 0;
            }
        }

        var result = await _client.PurgeQueueAsync(_purgeOptions.Queue, cancellationToken);

        if (!result.IsSuccess)
        {
            _statusOutput.ShowError($"Failed to purge queue {formattedQueueName} in vhost {formattedVhostName}", result.ErrorInfo);
            return 1;
        }
        
        var response = new PurgeResponse
        {
            Queue = _purgeOptions.Queue,
            Vhost = _rabbitMqConfig.VirtualHost,
            Status = result.IsSuccess ? "success" : "error",
            Timestamp = DateTime.Now
        };

        _resultOutput.Write(response);

        return 0;
    }
}