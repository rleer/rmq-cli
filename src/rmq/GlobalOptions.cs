using System.CommandLine;

namespace Rmq;

/// <summary>
/// Scripts branch on these, so they are part of the contract. See CLAUDE.md.
/// </summary>
public static class ExitCode
{
    public const int Success = 0;
    public const int Connection = 1;
    public const int Usage = 2;
    public const int Incomplete = 3;
    public const int Interrupted = 130;
}

/// <summary>
/// The options every command shares. They live here as single instances because
/// ParseResult.GetValue looks options up by identity — a copy would always read null.
/// </summary>
public static class GlobalOptions
{
    public static readonly Option<string?> Url = new("--url")
    {
        Description = "Broker URL: amqp://user:pass@host:port/vhost (also amqps, http, https). Falls back to $RMQ_URL.",
        Recursive = true
    };

    public static readonly Option<string?> Host = new("--host") { Description = "Broker host (default: localhost)", Recursive = true };
    public static readonly Option<int?> Port = new("--port") { Description = "Broker port (default: 5672, or 5671 with TLS)", Recursive = true };
    public static readonly Option<string?> VirtualHost = new("--vhost") { Description = "Virtual host (default: /)", Recursive = true };
    public static readonly Option<string?> User = new("--user", "-u") { Description = "Username (default: guest)", Recursive = true };
    public static readonly Option<string?> Password = new("--password", "-p") { Description = "Password (default: guest)", Recursive = true };

    public static readonly Option<Transport?> Transport = new("--transport")
    {
        Description = "amqp (default) or http. The HTTP Management API is a degraded fallback for networks where only 80/443 reach the broker.",
        Recursive = true
    };

    public static readonly Option<int?> ManagementPort = new("--management-port")
    {
        Description = "Management API port (default: 15672, or 15671 with TLS)",
        Recursive = true
    };

    public static readonly Option<bool> Insecure = new("--insecure")
    {
        Description = "Accept self-signed certificates and hostname mismatches. Dev brokers only.",
        Recursive = true
    };

    public static readonly Option<bool> Json = new("--json") { Description = "Force NDJSON output even on a terminal", Recursive = true };

    public static readonly Option<bool> Raw = new("--raw")
    {
        Description = "Write message bodies only, with no envelope and no separator between them",
        Recursive = true
    };

    public static readonly Option<bool> Verbose = new("--verbose", "-v") { Description = "Diagnostics on stderr", Recursive = true };

    public static void AddTo(RootCommand root)
    {
        root.Add(Url);
        root.Add(Host);
        root.Add(Port);
        root.Add(VirtualHost);
        root.Add(User);
        root.Add(Password);
        root.Add(Transport);
        root.Add(ManagementPort);
        root.Add(Insecure);
        root.Add(Json);
        root.Add(Raw);
        root.Add(Verbose);
    }

    /// <exception cref="ArgumentException">The URL is malformed or uses an unsupported scheme.</exception>
    public static ConnectionSettings Settings(ParseResult parse) => Connection.Resolve(
        url: parse.GetValue(Url),
        host: parse.GetValue(Host),
        port: parse.GetValue(Port),
        virtualHost: parse.GetValue(VirtualHost),
        user: parse.GetValue(User),
        password: parse.GetValue(Password),
        transport: parse.GetValue(Transport),
        managementPort: parse.GetValue(ManagementPort),
        insecure: parse.GetValue(Insecure));
}
