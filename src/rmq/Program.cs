using System.CommandLine;

namespace Rmq;

/// <summary>
/// The whole object graph, constructed by hand. No DI container — it costs startup
/// time and hides the wiring, both of which this tool is specifically avoiding.
/// </summary>
public static class Program
{
    private const string Rabbit = """
                                    (\(\
                                    (-.-)
                                   o(")(")
                                  """;

    public static async Task<int> Main(string[] args)
    {
        var root = new RootCommand($"{Rabbit}\nDeveloper-focused CLI for publishing and consuming RabbitMQ messages");

        // publish, consume, and purge are added here.

        return await root.Parse(args).InvokeAsync();
    }
}
