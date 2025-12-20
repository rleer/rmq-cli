using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Shared.Json;

namespace RmqCli.Infrastructure.RabbitMq;

/// <summary>
/// HTTP client for RabbitMQ Management API
/// </summary>
public class RabbitManagementClient : IRabbitManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly RabbitMqConfig _config;
    private readonly ILogger<RabbitManagementClient> _logger;

    public RabbitManagementClient(RabbitMqConfig config, ILogger<RabbitManagementClient> logger)
    {
        _config = config;
        _logger = logger;

        var baseUrl = new UriBuilder
        {
            Scheme = _config.UseTls ? "https" : "http",
            Host = string.IsNullOrWhiteSpace(_config.TlsServerName) ? _config.Host : _config.TlsServerName,
            Port = _config.ManagementPort
        };

        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl.ToString());

        // Configure basic authentication
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.User}:{_config.Password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        // Set Accept header to application/json
        // _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ManagementApiResponse> PurgeQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var encodedVhost = WebUtility.UrlEncode(_config.VirtualHost);
        var encodedQueueName = WebUtility.UrlEncode(queueName);
        var url = $"api/queues/{encodedVhost}/{encodedQueueName}/contents";

        _logger.LogDebug("Purging queue '{QueueName}' in vhost '{VHost}' via URL: {Url}", queueName, _config.VirtualHost, url);

        try
        {
            var response = await _httpClient.DeleteAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to purge queue: status code={StatusCode}, reason={Reason}", (int)response.StatusCode, response.ReasonPhrase);

                return await HandleErrorResponse(response, cancellationToken);
            }

            return new ManagementApiResponse
            {
                IsSuccess = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error while purging queue '{QueueName}' in vhost '{VHost}'", queueName, _config.VirtualHost);
            return BuildHttpErrorResponse(ex);
        }
    }
    
    private async Task<ManagementApiResponse> HandleErrorResponse(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Placeholder for potential future error handling logic
        ManagementApiErrorResponse? apiErrorResponse = null;
        try
        {
            apiErrorResponse =
                await response.Content.ReadFromJsonAsync(JsonSerializationContext.RelaxedEscaping.ManagementApiErrorResponse, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Failed to deserialize error response");
        }

        string errorPhrase;
        string? suggestionPhrase = null;
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                errorPhrase = "Queue not found";
                suggestionPhrase = "Please verify that the queue name and virtual host are correct";
                break;
            case HttpStatusCode.Unauthorized:
                if (apiErrorResponse == null || apiErrorResponse.Reason == "Not_Authorized")
                {
                    errorPhrase = "Unauthorized access to RabbitMQ Management API";
                    suggestionPhrase = "Please check your credentials";
                    break;
                }

                errorPhrase = apiErrorResponse.Reason;
                suggestionPhrase = "Please verify your user permissions";
                break;
            default:
                errorPhrase = "An unknown error occurred";
                break;
        }

        return new ManagementApiResponse
        {
            IsSuccess = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            ErrorInfo = new ErrorInfo
            {
                Error = errorPhrase,
                Suggestion = suggestionPhrase
            }
        };
    }
    
    private static ManagementApiResponse BuildHttpErrorResponse(HttpRequestException ex)
    {
        return new ManagementApiResponse
        {
            IsSuccess = false,
            StatusCode = 0,
            ErrorInfo = new ErrorInfo
            {
                Error = $"HTTP request error occurred: {ex.Message}",
                Suggestion = "Please check the RabbitMQ Management API endpoint and your network connection"
            }
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}