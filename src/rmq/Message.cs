using System.Text;
using System.Text.Json.Serialization;
using RabbitMQ.Client;

namespace Rmq;

/// <summary>
/// One NDJSON line: what consume writes and what publish reads. See docs/message-schema.md.
/// Body is held in its wire form — inline JSON, plain text, or base64 — and BodyBytes
/// is the decoded view. The wire/bytes conversion happens here rather than in a JSON
/// converter because BodyEncoding is a sibling field a property converter cannot see.
/// </summary>
public sealed record Message
{
    public const string Base64Encoding = "base64";

    [JsonConverter(typeof(BodyConverter))]
    public string Body { get; init; } = string.Empty;

    /// <summary>"base64" when Body holds base64-encoded binary; omitted otherwise.</summary>
    public string? BodyEncoding { get; init; }

    public MessageProperties? Properties { get; init; }

    public string? RoutingKey { get; init; }
    public string? Exchange { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Redelivered { get; init; }

    /// <summary>The body as the broker sees it. Round-trips byte-exactly for text and binary.</summary>
    [JsonIgnore]
    public byte[] BodyBytes => BodyEncoding == Base64Encoding
        ? Convert.FromBase64String(Body)
        : Encoding.UTF8.GetBytes(Body);

    public static Message FromBytes(
        ReadOnlySpan<byte> body,
        MessageProperties? properties = null,
        string? exchange = null,
        string? routingKey = null,
        bool redelivered = false)
    {
        var (wire, encoding) = EncodeBody(body);
        return new Message
        {
            Body = wire,
            BodyEncoding = encoding,
            Properties = properties,
            Exchange = exchange,
            RoutingKey = routingKey,
            Redelivered = redelivered
        };
    }

    /// <summary>
    /// Valid UTF-8 stays text (and is emitted inline if it parses as JSON — see BodyConverter);
    /// anything else becomes base64, which is the only way a non-UTF-8 body survives JSON.
    /// </summary>
    private static (string Wire, string? Encoding) EncodeBody(ReadOnlySpan<byte> body)
    {
        if (body.Length == 0)
        {
            return (string.Empty, null);
        }

        try
        {
            return (StrictUtf8.GetString(body), null);
        }
        catch (DecoderFallbackException)
        {
            return (Convert.ToBase64String(body), Base64Encoding);
        }
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}

/// <summary>
/// The AMQP 0-9-1 property set, less the deprecated clusterId. Headers live here rather
/// than at the root because they are message properties.
/// </summary>
public sealed record MessageProperties
{
    public string? ContentType { get; init; }
    public string? ContentEncoding { get; init; }
    public DeliveryModes? DeliveryMode { get; init; }
    public byte? Priority { get; init; }
    public string? CorrelationId { get; init; }
    public string? ReplyTo { get; init; }
    public string? Expiration { get; init; }
    public string? MessageId { get; init; }

    /// <summary>Unix seconds, matching AMQP's own timestamp encoding.</summary>
    public long? Timestamp { get; init; }

    public string? Type { get; init; }
    public string? UserId { get; init; }
    public string? AppId { get; init; }
    /// <summary>
    /// Values are strings, longs, doubles, or bools. AMQP hands back byte[] for longstr,
    /// which consume decodes at the boundary — see docs/message-schema.md.
    /// </summary>
    public Dictionary<string, object>? Headers { get; init; }

    /// <summary>
    /// False when the message carried no properties at all, so consume can emit nothing
    /// rather than an empty "properties":{} on every line.
    /// </summary>
    public bool HasAny() =>
        ContentType != null || ContentEncoding != null || DeliveryMode != null || Priority != null ||
        CorrelationId != null || ReplyTo != null || Expiration != null || MessageId != null ||
        Timestamp != null || Type != null || UserId != null || AppId != null ||
        Headers is { Count: > 0 };
}
