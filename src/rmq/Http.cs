using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;

namespace Rmq;

/// <summary>
/// The Management HTTP API boundary. Its sole purpose is to work in networks where the
/// AMQP port is blocked and only 80/443 reach the broker — see CLAUDE.md. It is a
/// degraded fallback, not a co-equal path: no push support, no delivery tags, and
/// therefore no ack-after-write guarantee.
/// </summary>
public static class Http
{
    /// <summary>
    /// Messages per /get, and therefore the data-loss window. ackmode is applied
    /// server-side before the response is sent, so every message in a batch is already
    /// gone from the broker by the time rmq starts writing it out; a crash mid-write
    /// loses the whole batch. Small on purpose — RabbitMQ's own docs call /get
    /// unsuitable for production or high-volume use.
    /// </summary>
    public const int BatchSize = 10;

    private const string AckAndDelete = "ack_requeue_false";
    private const string AckAndRequeue = "ack_requeue_true";
    private const string Base64Payload = "base64";

    public static HttpClient CreateClient(ConnectionSettings settings)
    {
        var handler = new HttpClientHandler { SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 };

        if (settings.Insecure)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        var client = new HttpClient(handler, disposeHandler: true);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.User}:{settings.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        Log.Debug($"using the management API at {settings.ManagementBaseUrl}");
        return client;
    }

    /// <summary>
    /// The vhost is a path segment, so the default vhost has to arrive as %2F. Built
    /// absolute rather than relative to a BaseAddress so nothing gets a second chance to
    /// canonicalize that escape back into a separator.
    /// </summary>
    public static Uri Url(ConnectionSettings settings, string path) =>
        new($"{settings.ManagementBaseUrl}/api/{path}", UriKind.Absolute);

    /// <summary>Whether the broker routed the message — the HTTP equivalent of mandatory.</summary>
    public static async Task<bool> PublishAsync(
        HttpClient client,
        ConnectionSettings settings,
        string exchange,
        string routingKey,
        Message message,
        CancellationToken ct)
    {
        var request = new HttpPublishRequest
        {
            Properties = ToHttpProperties(message.Properties),
            RoutingKey = routingKey,
            // Always base64. The API would take a plain string for a UTF-8 body, but then
            // publish would need its own copy of the is-this-valid-UTF-8 rule; base64 is
            // correct for every body and costs a third more bytes on a troubleshooting path.
            Payload = Convert.ToBase64String(message.BodyBytes),
            PayloadEncoding = Base64Payload
        };

        var url = Url(settings, $"exchanges/{Escape(settings.VirtualHost)}/{Escape(exchange)}/publish");

        using var response = await SendAsync(
            client, HttpMethod.Post, url,
            JsonSerializer.Serialize(request, HttpJsonContext.Default.HttpPublishRequest),
            settings, $"exchange '{exchange}'", ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync(stream, HttpJsonContext.Default.HttpPublishResponse, ct);

        return result?.Routed ?? false;
    }

    /// <summary>
    /// One /get. An empty list means the queue is empty; with <paramref name="requeue"/>
    /// it means nothing of the sort, because those messages go straight back — which is
    /// why the caller must not loop on that ackmode.
    /// </summary>
    public static async Task<List<Message>> GetAsync(
        HttpClient client,
        ConnectionSettings settings,
        string queue,
        int count,
        bool requeue,
        CancellationToken ct)
    {
        var request = new HttpGetRequest
        {
            Count = count,
            AckMode = requeue ? AckAndRequeue : AckAndDelete,
            // "auto" base64-encodes a body that is not valid UTF-8 rather than mangling it.
            // truncate is deliberately never sent: it would silently shorten bodies.
            Encoding = "auto"
        };

        var url = Url(settings, $"queues/{Escape(settings.VirtualHost)}/{Escape(queue)}/get");

        using var response = await SendAsync(
            client, HttpMethod.Post, url,
            JsonSerializer.Serialize(request, HttpJsonContext.Default.HttpGetRequest),
            settings, $"queue '{queue}'", ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var batch = await JsonSerializer.DeserializeAsync(stream, HttpJsonContext.Default.ListHttpGetResponse, ct) ?? [];

        var messages = new List<Message>(batch.Count);
        foreach (var item in batch)
        {
            messages.Add(ToMessage(item));
        }

        return messages;
    }

    /// <summary>
    /// DELETE /contents answers 204 with no body, so unlike AMQP's QueuePurgeAsync there
    /// is no count to report back.
    /// </summary>
    public static async Task PurgeAsync(HttpClient client, ConnectionSettings settings, string queue, CancellationToken ct)
    {
        var url = Url(settings, $"queues/{Escape(settings.VirtualHost)}/{Escape(queue)}/contents");

        using var response = await SendAsync(client, HttpMethod.Delete, url, body: null, settings, $"queue '{queue}'", ct);
    }

    private static string Escape(string segment) => Uri.EscapeDataString(segment);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        Uri url,
        string? body,
        ConnectionSettings settings,
        string what,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body != null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        Log.Debug($"{method} {url}");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new BrokerException($"could not reach the management API at {settings.ManagementBaseUrl}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new BrokerException($"the management API at {settings.ManagementBaseUrl} did not respond in time", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw new BrokerException(await ExplainAsync(response, settings, what, ct));
        }
    }

    /// <summary>
    /// Why the request failed, in one line a human can act on. Deliberately a second copy
    /// of the job Amqp.Explain does rather than a shared one — the two transports fail in
    /// entirely different ways and share no types.
    /// </summary>
    private static async Task<string> ExplainAsync(HttpResponseMessage response, ConnectionSettings settings, string what, CancellationToken ct)
    {
        var reason = await ReadReasonAsync(response, ct);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => $"authentication failed for user '{settings.User}' at {settings.ManagementBaseUrl}",
            HttpStatusCode.Forbidden => $"user '{settings.User}' may not access virtual host '{settings.VirtualHost}'",
            HttpStatusCode.NotFound when reason == "vhost_not_found" =>
                $"virtual host '{settings.VirtualHost}' does not exist on {settings.Host}",
            // A 404 with no JSON body is the management plugin not being enabled, not a
            // missing queue, so only a broker-shaped answer names what was looked for.
            HttpStatusCode.NotFound when reason != null =>
                $"NOT_FOUND - no {what} in vhost '{settings.VirtualHost}'",
            _ => $"management API returned {(int)response.StatusCode} {response.ReasonPhrase}{(reason == null ? "" : $": {reason}")}"
        };
    }

    private static async Task<string?> ReadReasonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/json")
        {
            return null;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var error = await JsonSerializer.DeserializeAsync(stream, HttpJsonContext.Default.HttpError, ct);
            return error?.Reason ?? error?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Message ToMessage(HttpGetResponse item)
    {
        // /get returns the body as a plain string only when it is valid UTF-8 and
        // base64-encodes it otherwise. Ignoring that silently corrupts binary bodies.
        var body = item.PayloadEncoding == Base64Payload
            ? Convert.FromBase64String(item.Payload)
            : Encoding.UTF8.GetBytes(item.Payload);

        return Message.FromBytes(body, ToProperties(item.Properties), item.Exchange, item.RoutingKey, item.Redelivered);
    }

    /// <summary>
    /// A message with no properties reports them as a JSON *array*, because an empty
    /// Erlang proplist encodes as [] rather than {}. Deserializing straight into a record
    /// would therefore throw on most messages, which is why this takes a JsonElement.
    /// </summary>
    private static MessageProperties? ToProperties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var properties = element.Deserialize(HttpJsonContext.Default.HttpProperties);
        if (properties == null)
        {
            return null;
        }

        var converted = new MessageProperties
        {
            ContentType = properties.ContentType,
            ContentEncoding = properties.ContentEncoding,
            DeliveryMode = properties.DeliveryMode is { } mode ? (DeliveryModes)mode : null,
            Priority = properties.Priority,
            CorrelationId = properties.CorrelationId,
            ReplyTo = properties.ReplyTo,
            Expiration = properties.Expiration,
            MessageId = properties.MessageId,
            Timestamp = properties.Timestamp,
            Type = properties.Type,
            UserId = properties.UserId,
            AppId = properties.AppId,
            // The API hands back real JSON types, so there is no byte[] trap here — but the
            // same closed set still has to come out, nested shapes included.
            Headers = properties.Headers is { Count: > 0 } headers ? MessageJson.NormalizeHeaders(headers) : null
        };

        return converted.HasAny() ? converted : null;
    }

    private static HttpProperties ToHttpProperties(MessageProperties? properties) => new()
    {
        ContentType = properties?.ContentType,
        ContentEncoding = properties?.ContentEncoding,
        DeliveryMode = properties?.DeliveryMode is { } mode ? (int)mode : null,
        Priority = properties?.Priority,
        CorrelationId = properties?.CorrelationId,
        ReplyTo = properties?.ReplyTo,
        Expiration = properties?.Expiration,
        MessageId = properties?.MessageId,
        Timestamp = properties?.Timestamp,
        Type = properties?.Type,
        UserId = properties?.UserId,
        AppId = properties?.AppId,
        Headers = properties?.Headers is { Count: > 0 } headers ? headers : null
    };
}

internal sealed record HttpPublishRequest
{
    public required HttpProperties Properties { get; init; }
    public required string RoutingKey { get; init; }
    public required string Payload { get; init; }
    public required string PayloadEncoding { get; init; }
}

internal sealed record HttpPublishResponse
{
    public bool Routed { get; init; }
}

internal sealed record HttpGetRequest
{
    public required int Count { get; init; }

    /// <summary>The API spells this one word, so the naming policy cannot produce it.</summary>
    [JsonPropertyName("ackmode")]
    public required string AckMode { get; init; }

    public required string Encoding { get; init; }
}

internal sealed record HttpGetResponse
{
    /// <summary>JsonElement because an empty property set arrives as [] — see Http.ToProperties.</summary>
    public JsonElement Properties { get; init; }

    public string Payload { get; init; } = string.Empty;
    public string PayloadEncoding { get; init; } = string.Empty;
    public string Exchange { get; init; } = string.Empty;
    public string RoutingKey { get; init; } = string.Empty;
    public bool Redelivered { get; init; }
}

/// <summary>The AMQP property set as the management API spells it: snake_case, mode as a number.</summary>
internal sealed record HttpProperties
{
    public string? ContentType { get; init; }
    public string? ContentEncoding { get; init; }
    public int? DeliveryMode { get; init; }
    public byte? Priority { get; init; }
    public string? CorrelationId { get; init; }
    public string? ReplyTo { get; init; }
    public string? Expiration { get; init; }
    public string? MessageId { get; init; }
    public long? Timestamp { get; init; }
    public string? Type { get; init; }
    public string? UserId { get; init; }
    public string? AppId { get; init; }
    public Dictionary<string, object>? Headers { get; init; }
}

internal sealed record HttpError
{
    public string? Error { get; init; }
    public string? Reason { get; init; }
}

[JsonSerializable(typeof(HttpPublishRequest))]
[JsonSerializable(typeof(HttpPublishResponse))]
[JsonSerializable(typeof(HttpGetRequest))]
[JsonSerializable(typeof(List<HttpGetResponse>))]
[JsonSerializable(typeof(HttpProperties))]
[JsonSerializable(typeof(HttpError))]
// Header values are polymorphic, so every shape one can take has to be registered here
// too — otherwise a header-carrying publish fails at runtime on this path alone.
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(object))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class HttpJsonContext : JsonSerializerContext;
