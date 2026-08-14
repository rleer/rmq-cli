namespace Rmq;

public enum Transport
{
    Amqp,
    Http
}

/// <summary>
/// A fully resolved broker target. Two ports because two of them genuinely exist:
/// AmqpPort is what RabbitMQ.Client dials, ManagementPort is what the HTTP fallback dials.
/// </summary>
public sealed record ConnectionSettings
{
    public required string Host { get; init; }
    public required int AmqpPort { get; init; }
    public required int ManagementPort { get; init; }
    public required string VirtualHost { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }

    /// <summary>Set by the amqps:// and https:// schemes. There is no separate --tls flag.</summary>
    public required bool UseTls { get; init; }

    public required Transport Transport { get; init; }

    /// <summary>Accept self-signed certificates and hostname mismatches. Dev brokers only.</summary>
    public required bool Insecure { get; init; }

    public string ManagementBaseUrl => $"{(UseTls ? "https" : "http")}://{Host}:{ManagementPort}";

    /// <summary>Safe to log: password redacted.</summary>
    public string Describe() => Transport == Transport.Http
        ? $"{ManagementBaseUrl} vhost={VirtualHost} user={User}"
        : $"{(UseTls ? "amqps" : "amqp")}://{User}@{Host}:{AmqpPort}{(VirtualHost == "/" ? "/" : "/" + VirtualHost)}";
}

/// <summary>
/// Resolves the one connection knob: individual flags beat --url, which beats $RMQ_URL,
/// which beats the defaults. Precedence is applied per component, so
/// `--url amqp://prod/ --vhost /test` is meaningful.
/// </summary>
public static class Connection
{
    public const string UrlEnvironmentVariable = "RMQ_URL";

    private const string DefaultHost = "localhost";
    private const string DefaultVirtualHost = "/";
    private const string DefaultUser = "guest";
    private const string DefaultPassword = "guest";
    private const int DefaultManagementPort = 15672;

    public static ConnectionSettings Resolve(
        string? url = null,
        string? host = null,
        int? port = null,
        string? virtualHost = null,
        string? user = null,
        string? password = null,
        Transport? transport = null,
        int? managementPort = null,
        bool insecure = false,
        Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;

        var urlText = url ?? environment(UrlEnvironmentVariable);
        var parsed = string.IsNullOrWhiteSpace(urlText) ? null : ParseUrl(urlText);

        var scheme = parsed?.Scheme ?? UrlScheme.Amqp;
        var useTls = scheme is UrlScheme.Amqps or UrlScheme.Https;
        var schemeIsHttp = scheme is UrlScheme.Http or UrlScheme.Https;

        // http(s):// points at the Management API directly — the blocked-AMQP-port case.
        var resolvedTransport = transport ?? (schemeIsHttp ? Transport.Http : Transport.Amqp);

        // --port overrides whichever port the URL scheme designates; that is the port the
        // user typed next to the host, whatever it addresses.
        var urlPort = port ?? parsed?.Port;

        int amqpPort;
        int resolvedManagementPort;
        if (schemeIsHttp)
        {
            // The URL addresses the Management API, so its port is the management port.
            amqpPort = DefaultPortFor(useTls ? UrlScheme.Amqps : UrlScheme.Amqp);
            resolvedManagementPort = managementPort ?? urlPort ?? DefaultPortFor(scheme);
        }
        else
        {
            // With an amqp(s):// URL the management API is on the same host at 15672,
            // over https only because the amqps scheme said so.
            amqpPort = urlPort ?? DefaultPortFor(scheme);
            resolvedManagementPort = managementPort ?? DefaultManagementPort;
        }

        return new ConnectionSettings
        {
            Host = host ?? parsed?.Host ?? DefaultHost,
            AmqpPort = amqpPort,
            ManagementPort = resolvedManagementPort,
            VirtualHost = virtualHost ?? parsed?.VirtualHost ?? DefaultVirtualHost,
            User = user ?? parsed?.User ?? DefaultUser,
            Password = password ?? parsed?.Password ?? DefaultPassword,
            UseTls = useTls,
            Transport = resolvedTransport,
            Insecure = insecure
        };
    }

    private enum UrlScheme
    {
        Amqp,
        Amqps,
        Http,
        Https
    }

    private static int DefaultPortFor(UrlScheme scheme) => scheme switch
    {
        UrlScheme.Amqp => 5672,
        UrlScheme.Amqps => 5671,
        UrlScheme.Http => 15672,
        UrlScheme.Https => 443,
        _ => 5672
    };

    private sealed record ParsedUrl(UrlScheme Scheme, string? Host, int? Port, string? VirtualHost, string? User, string? Password);

    /// <exception cref="ArgumentException">The URL is malformed or uses an unsupported scheme.</exception>
    private static ParsedUrl ParseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid broker URL '{url}': expected amqp://, amqps://, http://, or https://");
        }

        var scheme = uri.Scheme.ToLowerInvariant() switch
        {
            "amqp" => UrlScheme.Amqp,
            "amqps" => UrlScheme.Amqps,
            "http" => UrlScheme.Http,
            "https" => UrlScheme.Https,
            _ => throw new ArgumentException($"Unsupported URL scheme '{uri.Scheme}': expected amqp, amqps, http, or https")
        };

        string? user = null;
        string? password = null;
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            user = Uri.UnescapeDataString(separator < 0 ? uri.UserInfo : uri.UserInfo[..separator]);
            password = separator < 0 ? null : Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]);
        }

        return new ParsedUrl(
            scheme,
            string.IsNullOrEmpty(uri.Host) ? null : uri.Host,
            uri.Port < 0 || uri.IsDefaultPort && !HasExplicitPort(url) ? null : uri.Port,
            ParseVirtualHost(uri),
            user,
            password);
    }

    /// <summary>
    /// Uri fills in a default port for known schemes, so an http:// URL without one reports
    /// 80 rather than nothing. Only the text can say whether the user typed a port.
    /// </summary>
    private static bool HasExplicitPort(string url)
    {
        var afterScheme = url.IndexOf("//", StringComparison.Ordinal);
        if (afterScheme < 0)
        {
            return false;
        }

        var authority = url[(afterScheme + 2)..];
        var end = authority.IndexOfAny(['/', '?', '#']);
        if (end >= 0)
        {
            authority = authority[..end];
        }

        var at = authority.LastIndexOf('@');
        if (at >= 0)
        {
            authority = authority[(at + 1)..];
        }

        // Skip the colons inside an IPv6 literal.
        var closingBracket = authority.LastIndexOf(']');
        var colon = authority.LastIndexOf(':');
        return colon > closingBracket;
    }

    /// <summary>
    /// An empty path means the default vhost. This deviates from the AMQP URI spec, where
    /// `amqp://host/` means the *empty* vhost — a trap that has cost people hours and buys
    /// a dev CLI nothing. Use %2f to name a vhost containing a slash.
    /// </summary>
    private static string? ParseVirtualHost(Uri uri)
    {
        var path = uri.AbsolutePath;
        if (path.StartsWith('/'))
        {
            path = path[1..];
        }

        return path.Length == 0 ? null : Uri.UnescapeDataString(path);
    }
}
