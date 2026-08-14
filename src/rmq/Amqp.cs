using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Rmq;

/// <summary>
/// The broker refused us or could not be reached. The one exception type here, because
/// it is the boundary that decides exit code 1 — everything else is a normal failure.
/// </summary>
public sealed class BrokerException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The AMQP boundary: opening a connection, and translating between RabbitMQ.Client's
/// types and the wire schema in docs/message-schema.md.
/// </summary>
public static class Amqp
{
    /// <summary>
    /// Broker QoS for the push path. Internal on purpose — see CLAUDE.md; exposing it as
    /// a flag bought nothing but validation. Zero (unlimited) under --requeue, or the
    /// broker sends prefetch-many, gets no acks, and delivery stalls mid-queue.
    /// </summary>
    public const ushort PrefetchCount = 100;

    /// <summary>How many deliveries the push adapter buffers between callback and loop.</summary>
    public const int BufferSize = 100;

    public static async Task<IConnection> ConnectAsync(ConnectionSettings settings, CancellationToken ct)
    {
        // TODO(phase 5): the HTTP transport routes around this entirely. Until then, say so
        // rather than silently connecting over AMQP and ignoring the flag. ArgumentException
        // on purpose — this exits 2, because nothing is wrong with the broker.
        if (settings.Transport == Transport.Http)
        {
            throw new ArgumentException("--transport http is not implemented yet");
        }

        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.AmqpPort,
            UserName = settings.User,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            ClientProvidedName = "rmq"
        };

        if (settings.UseTls)
        {
            factory.Ssl = new SslOption
            {
                Enabled = true,
                ServerName = settings.Host,
                Version = SslProtocols.Tls12 | SslProtocols.Tls13,
                AcceptablePolicyErrors = settings.Insecure
                    ? SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch
                    : SslPolicyErrors.None
            };
        }

        Log.Debug($"connecting to {settings.Describe()}");

        try
        {
            return await factory.CreateConnectionAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new BrokerException(Explain(ex, settings), ex);
        }
    }

    /// <summary>
    /// Why the connection failed, in one line a human can act on. The specific reason is
    /// usually wrapped — an auth failure arrives inside a BrokerUnreachableException — so
    /// the chain is walked rather than just the outermost type inspected.
    /// </summary>
    private static string Explain(Exception exception, ConnectionSettings settings)
    {
        var target = $"{settings.Host}:{settings.AmqpPort}";

        return MostSpecific(exception) switch
        {
            AuthenticationFailureException => $"authentication failed for user '{settings.User}' at {target}",
            OperationInterruptedException interrupted => ExplainShutdown(interrupted, settings),
            ConnectFailureException => $"could not connect to {target}",
            BrokerUnreachableException => $"broker unreachable at {target}",
            var other => $"connection to {target} failed: {other.Message}"
        };
    }

    private static string ExplainShutdown(OperationInterruptedException exception, ConnectionSettings settings)
    {
        var code = exception.ShutdownReason?.ReplyCode ?? 0;
        var text = exception.ShutdownReason?.ReplyText ?? exception.Message;

        // 530 NOT_ALLOWED covers both "no such vhost" and "you may not have it", and only
        // the reply text tells them apart.
        if (code == 530)
        {
            return text.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? $"virtual host '{settings.VirtualHost}' does not exist on {settings.Host}:{settings.AmqpPort}"
                : $"user '{settings.User}' may not access virtual host '{settings.VirtualHost}'";
        }

        return $"broker refused the connection: {text} (code {code})";
    }

    private static Exception MostSpecific(Exception exception)
    {
        // Most specific first; a lower index wins.
        var priority = new[]
        {
            typeof(AuthenticationFailureException),
            typeof(OperationInterruptedException),
            typeof(ConnectFailureException),
            typeof(BrokerUnreachableException)
        };

        Exception? best = null;
        var bestRank = int.MaxValue;

        for (var current = exception; current != null; current = current.InnerException)
        {
            var rank = Array.IndexOf(priority, current.GetType());
            if (rank >= 0 && rank < bestRank)
            {
                best = current;
                bestRank = rank;
            }
        }

        return best ?? exception;
    }

    /// <summary>
    /// Properties as the schema wants them, or null when the message carried none — an
    /// empty "properties":{} would be noise on every line.
    /// </summary>
    public static MessageProperties? ToProperties(IReadOnlyBasicProperties properties)
    {
        var converted = new MessageProperties
        {
            ContentType = properties.IsContentTypePresent() ? properties.ContentType : null,
            ContentEncoding = properties.IsContentEncodingPresent() ? properties.ContentEncoding : null,
            DeliveryMode = properties.IsDeliveryModePresent() ? properties.DeliveryMode : null,
            Priority = properties.IsPriorityPresent() ? properties.Priority : null,
            CorrelationId = properties.IsCorrelationIdPresent() ? properties.CorrelationId : null,
            ReplyTo = properties.IsReplyToPresent() ? properties.ReplyTo : null,
            Expiration = properties.IsExpirationPresent() ? properties.Expiration : null,
            MessageId = properties.IsMessageIdPresent() ? properties.MessageId : null,
            Timestamp = properties.IsTimestampPresent() ? properties.Timestamp.UnixTime : null,
            Type = properties.IsTypePresent() ? properties.Type : null,
            UserId = properties.IsUserIdPresent() ? properties.UserId : null,
            AppId = properties.IsAppIdPresent() ? properties.AppId : null,
            Headers = properties.IsHeadersPresent() ? ToHeaders(properties.Headers) : null
        };

        return converted.HasAny() ? converted : null;
    }

    public static BasicProperties ToBasicProperties(MessageProperties? properties)
    {
        var basic = new BasicProperties();
        if (properties == null)
        {
            return basic;
        }

        if (properties.ContentType != null) basic.ContentType = properties.ContentType;
        if (properties.ContentEncoding != null) basic.ContentEncoding = properties.ContentEncoding;
        if (properties.DeliveryMode != null) basic.DeliveryMode = properties.DeliveryMode.Value;
        if (properties.Priority != null) basic.Priority = properties.Priority.Value;
        if (properties.CorrelationId != null) basic.CorrelationId = properties.CorrelationId;
        if (properties.ReplyTo != null) basic.ReplyTo = properties.ReplyTo;
        if (properties.Expiration != null) basic.Expiration = properties.Expiration;
        if (properties.MessageId != null) basic.MessageId = properties.MessageId;
        if (properties.Timestamp != null) basic.Timestamp = new AmqpTimestamp(properties.Timestamp.Value);
        if (properties.Type != null) basic.Type = properties.Type;
        if (properties.UserId != null) basic.UserId = properties.UserId;
        if (properties.AppId != null) basic.AppId = properties.AppId;

        if (properties.Headers is { Count: > 0 } headers)
        {
            basic.Headers = ToTable(headers);
        }

        return basic;
    }

    /// <summary>
    /// Generic variance is the whole reason this exists: RabbitMQ writes field tables as
    /// IDictionary&lt;string, object?&gt;, and Dictionary&lt;string, object&gt; is not one.
    /// </summary>
    private static Dictionary<string, object?> ToTable(Dictionary<string, object> headers)
    {
        var table = new Dictionary<string, object?>(headers.Count);
        foreach (var (key, value) in headers)
        {
            table[key] = ToTableValue(value);
        }

        return table;
    }

    private static object ToTableValue(object value) => value switch
    {
        Dictionary<string, object> table => ToTable(table),
        List<object> list => ToTableList(list),
        _ => value
    };

    private static List<object?> ToTableList(List<object> list)
    {
        var result = new List<object?>(list.Count);
        foreach (var item in list)
        {
            result.Add(ToTableValue(item));
        }

        return result;
    }

    private static Dictionary<string, object>? ToHeaders(IDictionary<string, object?>? headers)
    {
        if (headers is not { Count: > 0 })
        {
            return null;
        }

        var result = new Dictionary<string, object>(headers.Count);
        foreach (var (key, value) in headers)
        {
            result[key] = ToHeaderValue(value);
        }

        return result;
    }

    /// <summary>
    /// AMQP field tables carry types System.Text.Json cannot write, and two of them show up
    /// on ordinary messages: every longstr arrives as byte[] (so `x-source: web` would
    /// serialize to "d2Vi"), and a dead-lettered message's x-death header is a list of
    /// nested tables. Flatten the whole tree to the closed set the schema names — string,
    /// long, double, bool, object, array — so nothing unserializable can reach the writer.
    /// </summary>
    private static object ToHeaderValue(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        byte[] bytes => DecodeLongString(bytes),
        bool flag => flag,
        byte number => (long)number,
        sbyte number => (long)number,
        short number => (long)number,
        ushort number => (long)number,
        int number => (long)number,
        uint number => (long)number,
        long number => number,
        ulong number => (double)number,
        float number => (double)number,
        double number => number,
        decimal number => (double)number,
        AmqpTimestamp timestamp => timestamp.UnixTime,
        BinaryTableValue binary => Convert.ToBase64String(binary.Bytes),
        IDictionary<string, object?> table => ToHeaders(table) ?? new Dictionary<string, object>(),
        IList<object?> list => ToHeaderList(list),
        _ => value.ToString() ?? string.Empty
    };

    private static List<object> ToHeaderList(IList<object?> list)
    {
        var result = new List<object>(list.Count);
        foreach (var item in list)
        {
            result.Add(ToHeaderValue(item));
        }

        return result;
    }

    /// <summary>
    /// Header longstr values are almost always text, but nothing in AMQP requires it. Text
    /// stays text; anything else becomes base64 rather than corrupting into replacement
    /// characters. Deliberately the same rule as a message body, written out twice.
    /// </summary>
    private static string DecodeLongString(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Convert.ToBase64String(bytes);
        }
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}
