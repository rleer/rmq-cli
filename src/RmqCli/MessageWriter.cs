using System.Text;

namespace Rmq;

public enum OutputFormat
{
    /// <summary>One complete JSON object per line. The default when stdout is not a terminal.</summary>
    Ndjson,

    /// <summary>Hand-rolled human form with ANSI colour. The default on a TTY.</summary>
    Human,

    /// <summary>Message body bytes only, no envelope. For piping payloads into other tools.</summary>
    Raw
}

/// <summary>
/// Writes consumed messages to stdout or a file. Owns the flush, because the delivery
/// guarantee is "acked only after it is durably written" and the ack happens in the
/// consume loop immediately after FlushAsync returns.
/// </summary>
public sealed class MessageWriter : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly OutputFormat _format;
    private readonly bool _color;

    private MessageWriter(Stream stream, bool ownsStream, OutputFormat format, bool color)
    {
        _stream = stream;
        _ownsStream = ownsStream;
        _format = format;
        _color = color;
    }

    /// <summary>
    /// Shape follows the destination: a terminal gets the human form, a pipe or a file
    /// gets NDJSON. --json and --raw override that; --to-file is never a terminal.
    /// </summary>
    public static MessageWriter Create(bool json, bool raw, string? toFile)
    {
        var format = raw ? OutputFormat.Raw
            : json ? OutputFormat.Ndjson
            : toFile != null ? OutputFormat.Ndjson
            : Console.IsOutputRedirected ? OutputFormat.Ndjson
            : OutputFormat.Human;

        if (toFile != null)
        {
            var stream = new FileStream(toFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            return new MessageWriter(stream, ownsStream: true, format, color: false);
        }

        var color = format == OutputFormat.Human &&
                    Environment.GetEnvironmentVariable("NO_COLOR") is null &&
                    Environment.GetEnvironmentVariable("TERM") != "dumb";

        return new MessageWriter(Console.OpenStandardOutput(), ownsStream: false, format, color);
    }

    public async Task WriteAsync(Message message, CancellationToken ct = default)
    {
        var bytes = _format switch
        {
            OutputFormat.Raw => message.BodyBytes,
            OutputFormat.Ndjson => Utf8.GetBytes(MessageJson.Serialize(message) + "\n"),
            _ => Utf8.GetBytes(Human(message))
        };

        await _stream.WriteAsync(bytes, ct);
    }

    public Task FlushAsync(CancellationToken ct = default) => _stream.FlushAsync(ct);

    public async ValueTask DisposeAsync()
    {
        await _stream.FlushAsync();
        if (_ownsStream)
        {
            await _stream.DisposeAsync();
        }
    }

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private string Human(Message message)
    {
        var text = new StringBuilder();
        var properties = message.Properties;

        text.Append(Paint("\u001b[36m", message.RoutingKey is { Length: > 0 } key ? key : "(no routing key)"));
        if (message.Exchange is { Length: > 0 } exchange)
        {
            text.Append(Paint("\u001b[2m", $"  on {exchange}"));
        }

        if (message.Redelivered)
        {
            text.Append(Paint("\u001b[33m", "  redelivered"));
        }

        text.Append(Paint("\u001b[2m", $"  {Size(message.BodyBytes.Length)}"));
        text.Append('\n');

        foreach (var (label, value) in Fields(properties, message.BodyEncoding))
        {
            text.Append("  ").Append(Paint("\u001b[2m", label + ": ")).Append(value).Append('\n');
        }

        if (properties?.Headers is { Count: > 0 } headers)
        {
            foreach (var (name, value) in headers)
            {
                text.Append("  ").Append(Paint("\u001b[2m", name + ": ")).Append(value).Append('\n');
            }
        }

        text.Append("  ").Append(message.Body.ReplaceLineEndings("\n  ")).Append("\n\n");
        return text.ToString();
    }

    private static IEnumerable<(string Label, string Value)> Fields(MessageProperties? p, string? bodyEncoding)
    {
        if (bodyEncoding != null) yield return ("body-encoding", bodyEncoding);
        if (p == null) yield break;
        if (p.ContentType != null) yield return ("content-type", p.ContentType);
        if (p.ContentEncoding != null) yield return ("content-encoding", p.ContentEncoding);
        if (p.DeliveryMode != null) yield return ("delivery-mode", p.DeliveryMode.ToString()!);
        if (p.Priority != null) yield return ("priority", p.Priority.Value.ToString());
        if (p.CorrelationId != null) yield return ("correlation-id", p.CorrelationId);
        if (p.ReplyTo != null) yield return ("reply-to", p.ReplyTo);
        if (p.Expiration != null) yield return ("expiration", p.Expiration);
        if (p.MessageId != null) yield return ("message-id", p.MessageId);
        if (p.Timestamp != null) yield return ("timestamp", DateTimeOffset.FromUnixTimeSeconds(p.Timestamp.Value).ToString("u"));
        if (p.Type != null) yield return ("type", p.Type);
        if (p.UserId != null) yield return ("user-id", p.UserId);
        if (p.AppId != null) yield return ("app-id", p.AppId);
    }

    private static string Size(long bytes) => bytes < 1024
        ? $"{bytes} B"
        : bytes < 1024 * 1024
            ? $"{bytes / 1024.0:0.#} KB"
            : $"{bytes / (1024.0 * 1024.0):0.#} MB";

    private string Paint(string color, string text) => _color ? color + text + "\u001b[0m" : text;
}
